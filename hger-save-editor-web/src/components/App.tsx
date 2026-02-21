import { useState, useEffect, useCallback, useRef } from 'react';
import * as GameData from '../core/GameData';
import { BOX_COUNT, BOX_SLOT_COUNT } from '../core/SAV4HGSS';
import { useSaveFileStore } from '../stores/UseSaveFile';
import { MainView } from './MainView';

export default function App() {
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const { save, statusMessage, loadFile, setStatus } = useSaveFileStore();
  const fileInputRef = useRef<HTMLInputElement>(null);

  // ==================== GameData 초기화 ====================

  useEffect(() => {
    GameData.initialize('/data')
      .then(() => setLoading(false))
      .catch((err) => {
        console.error('GameData 초기화 실패:', err);
        setError('데이터 파일 로드에 실패했습니다.');
        setLoading(false);
      });
  }, []);

  // ==================== 파일 열기 ====================

  const handleFileSelect = useCallback((file: File) => {
    const ext = file.name.toLowerCase().split('.').pop();
    if (ext !== 'sav' && ext !== 'dsv') {
      alert('.sav 또는 .dsv 파일만 지원합니다.');
      return;
    }
    const reader = new FileReader();
    reader.onload = () => {
      if (reader.result instanceof ArrayBuffer) {
        loadFile(reader.result, file.name);
      }
    };
    reader.onerror = () => {
      alert('파일을 읽을 수 없습니다.');
    };
    reader.readAsArrayBuffer(file);
  }, [loadFile]);

  const handleInputChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) handleFileSelect(file);
    e.target.value = '';
  }, [handleFileSelect]);

  // ==================== 드래그 앤 드롭 ====================

  const handleDragOver = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    e.dataTransfer.dropEffect = 'copy';
  }, []);

  const handleDrop = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    const file = e.dataTransfer.files[0];
    if (file) handleFileSelect(file);
  }, [handleFileSelect]);

  // ==================== 저장 ====================

  const checkWarningsBeforeSave = useCallback((): boolean => {
    if (!save) return true;
    const locations: string[] = [];
    const count = save.partyCount;
    for (let i = 0; i < count; i++) {
      const pk = save.getPartySlot(i);
      if (!pk.isEmpty && !pk.isEgg && pk.hasWarnings) {
        locations.push(`파티 ${i + 1}번 - ${GameData.getSpeciesName(pk.species)}`);
      }
    }
    for (let box = 0; box < BOX_COUNT; box++) {
      for (let slot = 0; slot < BOX_SLOT_COUNT; slot++) {
        const pk = save.getBoxSlot(box, slot);
        if (!pk.isEmpty && !pk.isEgg && pk.hasWarnings) {
          let boxName = save.getBoxName(box).trim();
          if (!boxName || /^\d+$/.test(boxName)) boxName = `박스 ${box + 1}`;
          locations.push(`${boxName} ${slot + 1}번 - ${GameData.getSpeciesName(pk.species)}`);
        }
      }
    }
    if (locations.length === 0) return true;
    const show = locations.slice(0, 10);
    let list = show.join('\n');
    if (locations.length > 10) list += `\n... 외 ${locations.length - 10}건`;
    alert(`다음 포켓몬에 문제가 있어 저장할 수 없습니다:\n\n${list}\n\n해당 포켓몬을 수정한 후 다시 저장해 주세요.`);
    return false;
  }, [save]);

  const downloadBlob = useCallback((blob: Blob, fileName: string) => {
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    a.click();
    URL.revokeObjectURL(url);
  }, []);

  const handleSave = useCallback(() => {
    if (!save) { alert('열린 파일이 없습니다.'); return; }
    if (!checkWarningsBeforeSave()) return;
    downloadBlob(save.saveToBlob(), save.fileName || 'save.sav');
    setStatus(`저장 완료: ${save.fileName}`);
  }, [save, checkWarningsBeforeSave, downloadBlob, setStatus]);

  const handleSaveAs = useCallback(() => {
    if (!save) { alert('열린 파일이 없습니다.'); return; }
    if (!checkWarningsBeforeSave()) return;
    const name = prompt('파일 이름을 입력하세요:', save.fileName || 'save.sav');
    if (!name) return;
    downloadBlob(save.saveToBlob(), name);
    setStatus(`저장 완료: ${name}`);
  }, [save, checkWarningsBeforeSave, downloadBlob, setStatus]);

  // ==================== 키보드 단축키 ====================

  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.ctrlKey && e.key === 'o') {
        e.preventDefault();
        fileInputRef.current?.click();
      } else if (e.ctrlKey && e.key === 's') {
        e.preventDefault();
        handleSave();
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [handleSave]);

  // ==================== 로딩 / 에러 화면 ====================

  if (loading) {
    return (
      <div style={centerStyle}>
        <div style={{ color: '#C0C0C0', fontSize: '16px' }}>데이터 로드 중...</div>
      </div>
    );
  }

  if (error) {
    return (
      <div style={centerStyle}>
        <div style={{ color: '#FF6347', fontSize: '16px' }}>{error}</div>
      </div>
    );
  }

  // ==================== 메인 렌더링 ====================

  return (
    <div
      style={{ display: 'flex', flexDirection: 'column', height: '100%', maxWidth: 1010, margin: '0 auto' }}
      onDragOver={handleDragOver}
      onDrop={handleDrop}
    >
      {/* 메뉴 바 */}
      <div className="menu-bar" style={{ display: 'flex', alignItems: 'center', background: '#2D2D30', padding: '0 8px', height: 32, gap: 4 }}>
        <input ref={fileInputRef} type="file" accept=".sav,.dsv" onChange={handleInputChange} style={{ display: 'none' }} />
        <button onClick={() => fileInputRef.current?.click()} style={menuBtnStyle}>열기</button>
        <button onClick={handleSave} style={menuBtnStyle} disabled={!save}>저장</button>
        <button onClick={handleSaveAs} style={menuBtnStyle} disabled={!save}>다른 이름으로 저장</button>
        <div style={{ flex: 1 }} />
        <span className="menu-title" style={{ color: '#808080', fontSize: '12px' }}>HGER Save Editor Web</span>
      </div>

      {/* 메인 영역 */}
      <div style={{ flex: 1, overflow: 'hidden' }}>
        {save ? (
          <MainView />
        ) : (
          <div style={{ ...centerStyle, height: '100%' }}>
            <div style={{ textAlign: 'center', color: '#808080' }}>
              <div style={{ fontSize: '48px', marginBottom: 16 }}>📁</div>
              <div style={{ fontSize: '16px', marginBottom: 8 }}>세이브 파일을 열어주세요</div>
              <div style={{ fontSize: '13px' }}>.sav 또는 .dsv 파일을 드래그 앤 드롭하거나 열기 버튼을 클릭하세요.</div>
            </div>
          </div>
        )}
      </div>

      {/* 상태바 */}
      <div style={{ background: '#007ACC', padding: '3px 10px', color: '#FFF', fontSize: '12px', flexShrink: 0 }}>
        {statusMessage}
      </div>
    </div>
  );
}

const centerStyle: React.CSSProperties = {
  display: 'flex',
  alignItems: 'center',
  justifyContent: 'center',
  height: '100%',
};

const menuBtnStyle: React.CSSProperties = {
  background: 'transparent',
  color: '#C0C0C0',
  padding: '4px 12px',
  border: 'none',
  cursor: 'pointer',
  fontSize: '13px',
};
