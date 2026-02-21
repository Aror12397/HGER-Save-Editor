import { useState, useRef, useEffect, useMemo, useCallback } from 'react';

interface SearchComboBoxProps {
  items: readonly string[];
  selectedId: number;
  onSelect: (id: number) => void;
  width?: number;
  disabled?: boolean;
}

export function SearchComboBox({ items, selectedId, onSelect, width = 200, disabled = false }: SearchComboBoxProps) {
  const [search, setSearch] = useState('');
  const [isOpen, setIsOpen] = useState(false);
  const [isComposing, setIsComposing] = useState(false);
  const [highlightIndex, setHighlightIndex] = useState(-1);
  const inputRef = useRef<HTMLInputElement>(null);
  const listRef = useRef<HTMLUListElement>(null);
  const containerRef = useRef<HTMLDivElement>(null);

  const filtered = useMemo(() => {
    const result: { id: number; name: string }[] = [];
    for (let i = 0; i < items.length; i++) {
      const name = items[i] ?? '';
      if (!search || name.toLowerCase().includes(search.toLowerCase())) {
        result.push({ id: i, name });
      }
    }
    return result;
  }, [items, search]);

  useEffect(() => {
    setHighlightIndex(-1);
  }, [filtered]);

  useEffect(() => {
    if (highlightIndex >= 0 && listRef.current) {
      const el = listRef.current.children[highlightIndex] as HTMLElement | undefined;
      el?.scrollIntoView({ block: 'nearest' });
    }
  }, [highlightIndex]);

  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setIsOpen(false);
      }
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  const handleSelect = useCallback((id: number) => {
    onSelect(id);
    setIsOpen(false);
    setSearch('');
    inputRef.current?.blur();
  }, [onSelect]);

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (!isOpen) return;
    if (e.key === 'ArrowDown') {
      e.preventDefault();
      setHighlightIndex(h => Math.min(h + 1, filtered.length - 1));
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      setHighlightIndex(h => Math.max(h - 1, 0));
    } else if (e.key === 'Enter') {
      e.preventDefault();
      if (highlightIndex >= 0 && highlightIndex < filtered.length) {
        handleSelect(filtered[highlightIndex]!.id);
      }
    } else if (e.key === 'Escape') {
      setIsOpen(false);
      setSearch('');
    }
  };

  const displayValue = isOpen ? search : (items[selectedId] ?? '');

  return (
    <div ref={containerRef} className="search-combo" style={{ width, position: 'relative' }}>
      <input
        ref={inputRef}
        value={displayValue}
        disabled={disabled}
        onChange={(e) => {
          if (isComposing) return;
          setSearch(e.target.value);
          if (!isOpen) setIsOpen(true);
        }}
        onCompositionStart={() => setIsComposing(true)}
        onCompositionEnd={(e) => {
          setIsComposing(false);
          setSearch((e.target as HTMLInputElement).value);
        }}
        onFocus={() => { setSearch(''); setIsOpen(true); }}
        onKeyDown={handleKeyDown}
        style={{ width: '100%', boxSizing: 'border-box' }}
      />
      {isOpen && !disabled && (
        <ul
          ref={listRef}
          className="search-combo-dropdown"
          style={{
            position: 'absolute',
            top: '100%',
            left: 0,
            right: 0,
            maxHeight: 200,
            overflowY: 'auto',
            zIndex: 1000,
            listStyle: 'none',
            margin: 0,
            padding: 0,
            background: '#2D2D30',
            border: '1px solid #505050',
          }}
        >
          {filtered.map((item, i) => (
            <li
              key={item.id}
              onMouseDown={(e) => { e.preventDefault(); handleSelect(item.id); }}
              style={{
                padding: '3px 8px',
                cursor: 'pointer',
                background: i === highlightIndex ? '#094771' : 'transparent',
                color: '#FFF',
                fontSize: '13px',
              }}
              onMouseEnter={() => setHighlightIndex(i)}
            >
              {item.name}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
