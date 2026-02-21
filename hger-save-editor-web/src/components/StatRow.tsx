interface StatRowProps {
  name: string;
  baseStat: number | null;
  iv: number;
  ev: number;
  calcValue: number | null;
  nameColor?: string;
  onIVChange: (iv: number) => void;
  onEVChange: (ev: number) => void;
}

export function StatRow({ name, baseStat, iv, ev, calcValue, nameColor, onIVChange, onEVChange }: StatRowProps) {
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 4 }}>
      <span style={{ width: 50, color: nameColor ?? '#C0C0C0', fontSize: '13px', fontWeight: nameColor ? 'bold' : 'normal' }}>
        {name}
      </span>
      <span style={{ width: 35, textAlign: 'right', color: '#808080', fontSize: '13px' }}>
        {baseStat !== null ? baseStat : '-'}
      </span>
      <input
        type="text"
        value={iv}
        onChange={(e) => {
          const v = parseInt(e.target.value, 10);
          if (!isNaN(v)) onIVChange(Math.min(31, Math.max(0, v)));
        }}
        style={{ width: 40, textAlign: 'right' }}
        maxLength={2}
      />
      <input
        type="text"
        value={ev}
        onChange={(e) => {
          const v = parseInt(e.target.value, 10);
          if (!isNaN(v)) onEVChange(Math.min(252, Math.max(0, v)));
        }}
        style={{ width: 45, textAlign: 'right' }}
        maxLength={3}
      />
      <span style={{ width: 40, textAlign: 'right', color: '#FFF', fontSize: '13px' }}>
        {calcValue !== null ? calcValue : '-'}
      </span>
    </div>
  );
}
