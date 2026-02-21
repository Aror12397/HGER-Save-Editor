import { useState } from 'react';
import type { PK4 } from '../core/PK4';
import * as GameData from '../core/GameData';

interface SlotButtonProps {
  pk: PK4;
  onClick: () => void;
  onContextMenu: (e: React.MouseEvent) => void;
}

const SLOT_STYLE: React.CSSProperties = {
  width: 152,
  height: 80,
  margin: 4,
  border: '1px solid #505050',
  cursor: 'pointer',
  display: 'flex',
  alignItems: 'center',
  padding: '4px',
  fontFamily: "'Segoe UI', sans-serif",
  fontSize: '12px',
  position: 'relative',
  textAlign: 'left',
  flexShrink: 0,
};

export function SlotButton({ pk, onClick, onContextMenu }: SlotButtonProps) {
  const [spriteFailed, setSpriteFailed] = useState(false);

  if (pk.isEmpty) {
    return (
      <button
        className="slot-btn"
        style={{ ...SLOT_STYLE, background: '#37373A', color: '#808080', justifyContent: 'center' }}
        onClick={onClick}
        onContextMenu={(e) => { e.preventDefault(); onContextMenu(e); }}
      >
        (비어있음)
      </button>
    );
  }

  const isEgg = pk.isEgg;
  const speciesName = GameData.getSpeciesName(pk.species);
  let bgColor: string;
  let spriteUrl: string;
  let displayText: React.ReactNode;

  if (isEgg) {
    bgColor = '#325A3C';
    spriteUrl = GameData.getEggSpriteUrl();
    displayText = (
      <span style={{ whiteSpace: 'pre-line' }}>
        {`알\n(${speciesName})`}
      </span>
    );
  } else {
    bgColor = pk.isShiny ? '#645514' : '#284670';
    const formSpriteUrl = GameData.getSpriteUrl(pk.species, pk.form);
    const baseSpriteUrl = GameData.getSpriteUrl(pk.species, 0);
    spriteUrl = (spriteFailed && pk.form > 0) ? baseSpriteUrl : formSpriteUrl;

    const gender = pk.gender === 0 ? ' ♂' : pk.gender === 1 ? ' ♀' : '';
    const shiny = pk.isShiny ? ' ★' : '';
    displayText = (
      <span style={{ whiteSpace: 'pre-line' }}>
        {`${speciesName}${shiny}\nLv.${pk.level}${gender}`}
      </span>
    );
  }

  const tooltip = isEgg ? '알은 편집할 수 없습니다.' :
    `기술1: ${GameData.getMoveName(pk.move1)}\n기술2: ${GameData.getMoveName(pk.move2)}\n기술3: ${GameData.getMoveName(pk.move3)}\n기술4: ${GameData.getMoveName(pk.move4)}`;

  return (
    <button
      className="slot-btn"
      style={{ ...SLOT_STYLE, background: bgColor, color: '#FFF' }}
      onClick={onClick}
      onContextMenu={(e) => { e.preventDefault(); onContextMenu(e); }}
      title={tooltip}
    >
      {spriteUrl && (
        <img
          src={spriteUrl}
          onError={() => setSpriteFailed(true)}
          loading="lazy"
          width={48}
          height={48}
          alt={speciesName}
          style={{ marginRight: 6, flexShrink: 0, imageRendering: 'auto' }}
        />
      )}
      {displayText}
      {!isEgg && pk.hasWarnings && (
        <div style={{
          position: 'absolute', top: 2, left: 2, width: 20, height: 20,
          borderRadius: '50%', background: '#DC3232',
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          fontWeight: 'bold', fontSize: '12px', color: '#FFF',
        }}>!</div>
      )}
    </button>
  );
}
