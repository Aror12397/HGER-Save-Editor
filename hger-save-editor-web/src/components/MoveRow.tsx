import { SearchComboBox } from './SearchComboBox';
import * as GameData from '../core/GameData';

interface MoveRowProps {
  slotNum: number;
  moveId: number;
  pp: number;
  ppUp: number;
  onMoveChange: (id: number) => void;
  onPPChange: (pp: number) => void;
  onPPUpChange: (ppUp: number) => void;
}

export function MoveRow({ slotNum, moveId, pp, ppUp, onMoveChange, onPPChange, onPPUpChange }: MoveRowProps) {
  const displayId = moveId === 0xFFFF ? 0 : moveId;

  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 6 }}>
      <span style={{ width: 50, color: '#C0C0C0', fontSize: '13px' }}>기술 {slotNum}</span>
      <SearchComboBox
        items={GameData.getMoveNames()}
        selectedId={displayId}
        onSelect={onMoveChange}
        width={200}
      />
      <input
        type="text"
        value={pp}
        onChange={(e) => {
          const v = parseInt(e.target.value, 10);
          if (!isNaN(v)) onPPChange(Math.min(99, Math.max(0, v)));
        }}
        style={{ width: 40, textAlign: 'right' }}
        maxLength={2}
        placeholder="PP"
      />
      <input
        type="text"
        value={ppUp}
        onChange={(e) => {
          const v = parseInt(e.target.value, 10);
          if (!isNaN(v)) onPPUpChange(Math.min(3, Math.max(0, v)));
        }}
        style={{ width: 30, textAlign: 'right' }}
        maxLength={1}
        placeholder="UP"
      />
    </div>
  );
}
