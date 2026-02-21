import { useState, useCallback, useMemo } from 'react';
import { PK4 } from '../core/PK4';
import * as PokeCrypto from '../core/PokeCrypto';
import * as GameData from '../core/GameData';
import { BOX_COUNT, BOX_SLOT_COUNT } from '../core/SAV4HGSS';
import { useSaveFileStore } from '../stores/UseSaveFile';
import { SlotButton } from './SlotButton';
import { ContextMenu, type ContextMenuItem } from './ContextMenu';
import { PokemonEditor } from './PokemonEditor';

type SlotSource = 'party' | 'box';

interface EditorTarget {
  source: SlotSource;
  slotIndex: number;
  boxIndex: number;
  pk: PK4;
}

interface ContextMenuState {
  x: number;
  y: number;
  items: ContextMenuItem[];
}

export function MainView() {
  const { save, currentBox, version, setCurrentBox, setStatus, clipboardRaw, clipboardName, setClipboard, notifyChange } = useSaveFileStore();
  const [activeTab, setActiveTab] = useState<'party' | 'box'>('party');
  const [editor, setEditor] = useState<EditorTarget | null>(null);
  const [ctxMenu, setCtxMenu] = useState<ContextMenuState | null>(null);

  // ==================== 슬롯 데이터 ====================

  const partySlots = useMemo(() => {
    if (!save) return [];
    const count = save.partyCount;
    const slots: PK4[] = [];
    for (let i = 0; i < 6; i++) {
      slots.push(i < count ? save.getPartySlot(i) : PK4.empty);
    }
    return slots;
  }, [save, version]);

  const boxSlots = useMemo(() => {
    if (!save) return [];
    const slots: PK4[] = [];
    for (let i = 0; i < BOX_SLOT_COUNT; i++) {
      slots.push(save.getBoxSlot(currentBox, i));
    }
    return slots;
  }, [save, currentBox, version]);

  const boxNames = useMemo(() => {
    if (!save) return [];
    const names: string[] = [];
    for (let i = 0; i < BOX_COUNT; i++) {
      const name = save.getBoxName(i).trim();
      const isDefault = !name || /^\d+$/.test(name);
      names.push(isDefault ? `박스 ${i + 1}` : name);
    }
    return names;
  }, [save, version]);

  const trainerInfo = useMemo(() => {
    if (!save) return '';
    const pt = save.playtime;
    const gender = save.gender === 0 ? '♂' : '♀';
    const h = String(pt.hours).padStart(2, '0');
    const m = String(pt.minutes).padStart(2, '0');
    const s = String(pt.seconds).padStart(2, '0');
    return `트레이너: ${save.trainerName} ${gender}  ID: ${String(save.tid).padStart(5, '0')}  소지금: ${save.money.toLocaleString()}₩  플레이: ${h}:${m}:${s}`;
  }, [save, version]);

  // ==================== 슬롯 클릭 ====================

  const handleSlotClick = useCallback((source: SlotSource, slotIndex: number, boxIndex: number) => {
    if (!save) return;
    const pk = source === 'party' ? save.getPartySlot(slotIndex) : save.getBoxSlot(boxIndex, slotIndex);
    if (pk.isEmpty) return;
    if (pk.isEgg) {
      alert('알은 편집할 수 없습니다.');
      return;
    }
    setEditor({ source, slotIndex, boxIndex, pk });
  }, [save]);

  // ==================== 에디터 확인/취소 ====================

  const handleEditorConfirm = useCallback((editedPk: PK4) => {
    if (!save || !editor) return;
    if (editor.source === 'party') {
      save.setPartySlot(editor.slotIndex, editedPk);
    } else {
      save.setBoxSlot(editor.boxIndex, editor.slotIndex, editedPk);
    }
    notifyChange();
    setEditor(null);
  }, [save, editor, notifyChange]);

  // ==================== 우클릭 메뉴 ====================

  const handleContextMenu = useCallback((e: React.MouseEvent, source: SlotSource, slotIndex: number, boxIndex: number) => {
    if (!save) return;
    const pk = source === 'party' ? save.getPartySlot(slotIndex) : save.getBoxSlot(boxIndex, slotIndex);
    const hasData = !pk.isEmpty;
    const hasClip = clipboardRaw !== null;
    if (!hasData && !hasClip) return;

    const items: ContextMenuItem[] = [];

    if (hasData) {
      const name = GameData.getSpeciesName(pk.species);

      items.push({
        label: '복사',
        onClick: () => {
          setClipboard(pk.writeToRaw(), name);
          setStatus(`복사됨: ${name}`);
        },
      });

      const canDelete = source !== 'party' || save.partyCount > 1;
      if (canDelete) {
        items.push({
          label: '삭제',
          onClick: () => {
            if (!confirm(`${name}을(를) 삭제하시겠습니까?`)) return;
            if (source === 'party') {
              deletePartySlot(save, slotIndex);
            } else {
              save.setBoxSlot(boxIndex, slotIndex, PK4.empty);
            }
            notifyChange();
            setStatus(`삭제됨: ${name}`);
          },
        });
      }
    }

    if (hasClip) {
      items.push({
        label: `붙여넣기 (${clipboardName})`,
        onClick: () => {
          if (hasData) {
            const existing = GameData.getSpeciesName(pk.species);
            if (!confirm(`이 슬롯에 ${existing}이(가) 있습니다.\n${clipboardName}(으)로 덮어쓰시겠습니까?`)) return;
          }
          pasteToSlot(save, source, slotIndex, boxIndex, clipboardRaw!);
          notifyChange();
          setStatus(`붙여넣기 완료: ${clipboardName}`);
        },
      });
    }

    setCtxMenu({ x: e.clientX, y: e.clientY, items });
  }, [save, clipboardRaw, clipboardName, setClipboard, setStatus, notifyChange]);

  // ==================== 렌더링 ====================

  if (!save) return null;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
      {/* 탭 헤더 */}
      <div style={{ display: 'flex', background: '#2D2D30', borderBottom: '1px solid #505050' }}>
        {(['party', 'box'] as const).map((tab) => (
          <button
            key={tab}
            onClick={() => setActiveTab(tab)}
            style={{
              padding: '8px 24px',
              background: activeTab === tab ? '#282828' : 'transparent',
              color: activeTab === tab ? '#FFF' : '#AAA',
              border: 'none',
              borderBottom: activeTab === tab ? '2px solid #007ACC' : '2px solid transparent',
              cursor: 'pointer',
              fontSize: '14px',
            }}
          >
            {tab === 'party' ? '파티' : '박스'}
          </button>
        ))}
      </div>

      {/* 탭 내용 */}
      <div style={{ flex: 1, overflow: 'auto', background: '#282828', padding: 10 }}>
        {activeTab === 'party' && (
          <div>
            <div className="trainer-info" style={{ color: '#C0C0C0', fontWeight: 'bold', fontSize: '13px', marginBottom: 12 }}>
              {trainerInfo}
            </div>
            <div className="party-slots" style={{ display: 'flex', flexWrap: 'wrap' }}>
              {partySlots.map((pk, i) => (
                <SlotButton
                  key={i}
                  pk={pk}
                  onClick={() => handleSlotClick('party', i, 0)}
                  onContextMenu={(e) => handleContextMenu(e, 'party', i, 0)}
                />
              ))}
            </div>
          </div>
        )}

        {activeTab === 'box' && (
          <div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 12, background: '#323232', padding: '6px 10px' }}>
              <span style={{ color: '#FFF', fontSize: '13px' }}>박스:</span>
              <select
                value={currentBox}
                onChange={(e) => setCurrentBox(Number(e.target.value))}
                style={{ width: 180 }}
              >
                {boxNames.map((name, i) => (
                  <option key={i} value={i}>{name}</option>
                ))}
              </select>
            </div>
            <div className="box-grid" style={{ display: 'grid', gridTemplateColumns: 'repeat(6, 1fr)', justifyItems: 'center' }}>
              {boxSlots.map((pk, i) => (
                <SlotButton
                  key={i}
                  pk={pk}
                  onClick={() => handleSlotClick('box', i, currentBox)}
                  onContextMenu={(e) => handleContextMenu(e, 'box', i, currentBox)}
                />
              ))}
            </div>
          </div>
        )}
      </div>

      {/* 포켓몬 에디터 모달 */}
      {editor && (
        <PokemonEditor
          pk={editor.pk}
          isPartyForm={editor.source === 'party'}
          onConfirm={handleEditorConfirm}
          onCancel={() => setEditor(null)}
          defaultOTName={save.trainerName}
          defaultTID={save.tid}
          defaultSID={save.sid}
          defaultGender={save.gender}
        />
      )}

      {/* 컨텍스트 메뉴 */}
      {ctxMenu && (
        <ContextMenu
          x={ctxMenu.x}
          y={ctxMenu.y}
          items={ctxMenu.items}
          onClose={() => setCtxMenu(null)}
        />
      )}
    </div>
  );
}

// ==================== 유틸 ====================

function deletePartySlot(save: NonNullable<ReturnType<typeof useSaveFileStore.getState>['save']>, slotIndex: number): void {
  const count = save.partyCount;
  for (let i = slotIndex; i < count - 1; i++) {
    const next = save.getPartySlot(i + 1);
    save.setPartySlot(i, next);
  }
  save.setPartySlot(count - 1, PK4.createBlank(PokeCrypto.SIZE_4PARTY));
}

function pasteToSlot(
  save: NonNullable<ReturnType<typeof useSaveFileStore.getState>['save']>,
  source: SlotSource,
  slotIndex: number,
  boxIndex: number,
  raw: Uint8Array,
): void {
  const cloned = new Uint8Array(raw);
  if (source === 'party') {
    const partyRaw = cloned.length < PokeCrypto.SIZE_4PARTY
      ? PokeCrypto.convertToParty(cloned)
      : cloned;
    const pk = new PK4(partyRaw);
    save.setPartySlot(slotIndex, pk);
  } else {
    const pk = new PK4(cloned);
    save.setBoxSlot(boxIndex, slotIndex, pk);
  }
}
