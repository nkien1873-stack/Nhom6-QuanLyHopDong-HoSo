using System.Collections.Concurrent;

namespace QLHopDongHoSo.API.Services;

// Đếm số lần đăng nhập sai theo cặp (tài khoản, IP) trong bộ nhớ – singleton.
// Không ghi DB: người lạ ở máy khác không làm khóa được tài khoản của chủ (khóa chỉ áp cho đúng máy sai).
public class LoginAttemptTracker
{
    public sealed class Attempts
    {
        private readonly object _gate = new();
        private int _failures;

        public DateTime? LockedUntil { get; private set; }

        public DateTime LastSeen { get; private set; } = DateTime.UtcNow;

        public void RegisterFailure(int maxFailed, TimeSpan lockout)
        {
            lock (_gate)
            {
                LastSeen = DateTime.UtcNow;
                if (++_failures >= maxFailed)
                {
                    _failures = 0;
                    LockedUntil = DateTime.UtcNow.Add(lockout);
                }
            }
        }
    }

    private readonly ConcurrentDictionary<string, Attempts> _items = new();
    private DateTime _lastCleanup = DateTime.UtcNow;

    private static string Key(string username, string ip) => $"{username.ToLowerInvariant()}|{ip}";

    public Attempts For(string username, string ip, TimeSpan lockout)
    {
        Cleanup(lockout);
        return _items.GetOrAdd(Key(username, ip), _ => new Attempts());
    }

    public void Clear(string username, string ip) => _items.TryRemove(Key(username, ip), out _);

    // Bỏ các mục cũ (không còn khóa, lâu không sai) để bộ nhớ không tăng mãi khi bị dò nhiều username
    private void Cleanup(TimeSpan lockout)
    {
        var now = DateTime.UtcNow;
        if (now - _lastCleanup < TimeSpan.FromMinutes(1))
        {
            return;
        }

        _lastCleanup = now;
        var keepFor = lockout > TimeSpan.FromMinutes(15) ? lockout : TimeSpan.FromMinutes(15);
        foreach (var (key, value) in _items)
        {
            if ((value.LockedUntil ?? DateTime.MinValue) < now && now - value.LastSeen > keepFor)
            {
                _items.TryRemove(key, out _);
            }
        }
    }
}
