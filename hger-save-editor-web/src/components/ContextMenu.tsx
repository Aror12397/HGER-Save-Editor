import { useEffect, useRef } from 'react';
import { createPortal } from 'react-dom';

export interface ContextMenuItem {
  label: string;
  onClick: () => void;
  disabled?: boolean;
}

interface ContextMenuProps {
  x: number;
  y: number;
  items: ContextMenuItem[];
  onClose: () => void;
}

export function ContextMenu({ x, y, items, onClose }: ContextMenuProps) {
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const handleClick = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) {
        onClose();
      }
    };
    document.addEventListener('mousedown', handleClick);
    return () => document.removeEventListener('mousedown', handleClick);
  }, [onClose]);

  return createPortal(
    <div
      ref={ref}
      style={{
        position: 'fixed',
        left: x,
        top: y,
        background: '#2D2D30',
        border: '1px solid #505050',
        zIndex: 2000,
        minWidth: 150,
        boxShadow: '0 4px 12px rgba(0,0,0,0.5)',
      }}
    >
      {items.map((item, i) => (
        <div
          key={i}
          onClick={() => {
            if (!item.disabled) {
              item.onClick();
              onClose();
            }
          }}
          style={{
            padding: '6px 16px',
            cursor: item.disabled ? 'default' : 'pointer',
            color: item.disabled ? '#808080' : '#FFF',
            fontSize: '13px',
          }}
          onMouseEnter={(e) => {
            if (!item.disabled) (e.currentTarget as HTMLElement).style.background = '#094771';
          }}
          onMouseLeave={(e) => {
            (e.currentTarget as HTMLElement).style.background = 'transparent';
          }}
        >
          {item.label}
        </div>
      ))}
    </div>,
    document.body
  );
}
