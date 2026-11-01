using Microsoft.Extensions.Caching.Memory;

namespace QLHopDongHoSo.API.Data;

// Số phiên bản dữ liệu: tăng mỗi khi SaveChanges ghi thành công (ApplicationDbContext).
// Các số liệu tổng hợp (tổng quan, báo cáo, số đếm theo tab...) được cache theo phiên bản này:
// nhiều người cùng xem thì chỉ tính một lần, còn vừa có thao tác ghi là cache cũ tự hết hiệu lực.
public static class DataVersion
{
    private static long _value;

    public static long Current => Interlocked.Read(ref _value);

    public static void Bump() => Interlocked.Increment(ref _value);

    // Khi nhiều người cùng ghi liên tục, số liệu tổng hợp được phép cũ tối đa 1 giây để không phải tính lại
    // sau MỖI lần ghi. Trình duyệt vừa ghi xong gửi header "X-Fresh" => luôn nhận số liệu mới nhất.
    public static readonly TimeSpan MaxStale = TimeSpan.FromSeconds(2);

    private static readonly AsyncLocal<bool> RequireFreshFlag = new();

    // Đặt theo từng request (middleware trong Program.cs)
    public static bool RequireFresh
    {
        get => RequireFreshFlag.Value;
        set => RequireFreshFlag.Value = value;
    }

    // Trong lúc một request đang tính lại, các request khác dùng kết quả trước đó (chưa quá 30 giây) thay vì
    // cùng chờ: trên dữ liệu lớn + ghi liên tục, một lần tính lại có thể mất vài giây (DB 100.000 hồ sơ).
    private static readonly TimeSpan MaxServeWhileRefreshing = TimeSpan.FromSeconds(30);

    private sealed class Entry<T>
    {
        public required long Version { get; init; }
        public required DateTime StartedAt { get; init; }
        public required Task<T> Task { get; init; }

        // Kết quả hoàn chỉnh gần nhất (của phiên bản trước) + thời điểm bắt đầu tính
        public T? LastGood { get; init; }
        public DateTime LastGoodAt { get; init; }
        public bool HasLastGood { get; init; }
    }

    private static readonly object CreateLock = new();

    // Lấy từ cache hoặc tính mới (giữ tối đa 60 giây).
    // - Chỉ MỘT request chạy truy vấn khi cache cũ; request đó chờ kết quả (dùng DbContext của chính nó).
    // - Các request khác trong lúc đó nhận kết quả trước (≤ 5 giây) thay vì cùng chờ – trừ khi gửi X-Fresh.
    public static async Task<T> GetOrCreateAsync<T>(this IMemoryCache cache, string key, Func<Task<T>> factory)
    {
        var version = Current;
        var now = DateTime.UtcNow;
        var fresh = RequireFresh;
        Entry<T> entry;
        TaskCompletionSource<T>? owner = null;

        lock (CreateLock)
        {
            cache.TryGetValue(key, out Entry<T>? existing);
            var done = existing is { Task.IsCompletedSuccessfully: true };
            var running = existing is { Task.IsCompleted: false };

            if (existing != null && !fresh && done && now - existing.StartedAt < MaxStale)
            {
                return existing.Task.Result; // tính xong chưa quá 1 giây (có thể trước lần ghi gần nhất)
            }

            if (existing != null && !fresh && running && now - existing.StartedAt < MaxServeWhileRefreshing)
            {
                // Đã có request khác đang tính lại: dùng kết quả trước đó nếu còn mới, không thì chờ chung
                if (existing.HasLastGood && now - existing.LastGoodAt < MaxServeWhileRefreshing)
                {
                    return existing.LastGood!;
                }

                entry = existing;
            }
            else if (existing != null && existing.Version == version && (done || running))
            {
                entry = existing; // đúng phiên bản hiện tại (đã xong hoặc đang tính)
            }
            else
            {
                owner = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
                entry = new Entry<T>
                {
                    Version = version,
                    StartedAt = now,
                    Task = owner.Task,
                    HasLastGood = done || existing?.HasLastGood == true,
                    LastGood = done ? existing!.Task.Result : existing is { HasLastGood: true } ? existing.LastGood : default,
                    LastGoodAt = done ? existing!.StartedAt : existing?.LastGoodAt ?? default
                };
                cache.Set(key, entry, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60),
                    Size = 1
                });
            }
        }

        if (owner == null)
        {
            return await entry.Task;
        }

        // Request này chịu trách nhiệm tính (ngoài khóa), các request khác chờ/dùng chung kết quả
        try
        {
            var value = await factory();
            owner.SetResult(value);
            return value;
        }
        catch (Exception ex)
        {
            cache.Remove(key); // lỗi thì lần sau tính lại
            owner.SetException(ex);
            throw;
        }
    }
}
