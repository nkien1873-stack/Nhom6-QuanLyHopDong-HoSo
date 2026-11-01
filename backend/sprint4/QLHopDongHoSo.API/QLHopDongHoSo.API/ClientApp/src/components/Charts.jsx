// Biểu đồ nhẹ bằng CSS – không cần thư viện. Có thể thay bằng Recharts/Chart.js nếu muốn.

export function BarChart({ data, series, height = 240 }) {
  const max = Math.max(...data.flatMap((d) => series.map((s) => d[s.key])))
  const top = Math.ceil(max / 40) * 40 || 40
  const ticks = [top, top * 0.75, top * 0.5, top * 0.25, 0]
  return (
    <div>
      <div className="legend" style={{ marginBottom: 14 }}>
        {series.map((s) => <span key={s.key}><i style={{ background: s.color }} />{s.label}</span>)}
      </div>
      <div className="bars" style={{ height }} role="img"
        aria-label={`Biểu đồ cột: ${data.map((d) => `${d.label} ${series.map((s) => `${s.label} ${d[s.key]}`).join(', ')}`).join('; ')}`}>
        <div className="bars-axis">{ticks.map((t) => <span key={t}>{Math.round(t)}</span>)}</div>
        <div className="bars-plot" style={{ gridTemplateColumns: `repeat(${data.length}, minmax(0, 1fr))` }}>
          {data.map((d, i) => (
            <div className="bars-col" key={d.label}>
              {series.map((s) => (
                <div key={s.key} className="b" title={`${s.label}: ${d[s.key]}`} style={{ height: `${(d[s.key] / top) * 100}%`, background: s.color }} />
              ))}
              <span className="lbl" style={i === data.length - 1 ? { color: 'var(--navy)', fontWeight: 600 } : undefined}>{d.label}</span>
            </div>
          ))}
        </div>
      </div>
    </div>
  )
}

export function Donut({ items, centerLabel = 'hồ sơ' }) {
  const total = items.reduce((a, b) => a + b.value, 0) || 1
  let acc = 0
  const stops = items.map((it) => {
    const from = (acc / total) * 100
    acc += it.value
    return `${it.color} ${from}% ${(acc / total) * 100}%`
  })
  return (
    <div className="donut-wrap">
      <div className="donut" style={{ background: `conic-gradient(${stops.join(',')})` }} role="img"
        aria-label={`Biểu đồ tròn: ${items.map((i) => `${i.label} ${i.value}`).join(', ')}`}>
        <div className="donut-hole"><b>{total}</b><small>{centerLabel}</small></div>
      </div>
      <div className="donut-legend">
        {items.map((it) => (
          <div key={it.label}><i className="dot" style={{ background: it.color, borderRadius: 3 }} />{it.label}<b>{Math.round((it.value / total) * 100)}%</b></div>
        ))}
      </div>
    </div>
  )
}

export function HBars({ items, format = (v) => v }) {
  const max = Math.max(...items.map((i) => i.value), 1)
  return (
    <div className="stack" style={{ gap: 14 }}>
      {items.map((it) => (
        <div className="hbar" key={it.label}>
          <div className="hbar-top"><span>{it.label}</span><b className="num">{format(it.value)}</b></div>
          <div className="hbar-track"><div className="hbar-fill" style={{ width: `${(it.value / max) * 100}%`, background: it.color }} /></div>
        </div>
      ))}
    </div>
  )
}
