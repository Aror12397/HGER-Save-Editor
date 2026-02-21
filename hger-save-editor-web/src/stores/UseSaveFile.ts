import { create } from 'zustand';
import { SAV4HGSS } from '../core/SAV4HGSS';

interface SaveFileState {
  save: SAV4HGSS | null;
  currentBox: number;
  statusMessage: string;
  clipboardRaw: Uint8Array | null;
  clipboardName: string | null;
  version: number;

  loadFile: (buffer: ArrayBuffer, fileName: string) => void;
  setCurrentBox: (box: number) => void;
  setStatus: (msg: string) => void;
  setClipboard: (raw: Uint8Array, name: string) => void;
  clearClipboard: () => void;
  notifyChange: () => void;
}

export const useSaveFileStore = create<SaveFileState>((set) => ({
  save: null,
  currentBox: 0,
  statusMessage: '세이브 파일을 열어주세요.',
  clipboardRaw: null,
  clipboardName: null,
  version: 0,

  loadFile: (buffer, fileName) => {
    const save = SAV4HGSS.loadFromArrayBuffer(buffer, fileName);
    if (save) {
      set({
        save,
        currentBox: save.currentBox,
        statusMessage: `로드됨: ${fileName}  |  ${save.getBlockInfo()}`,
        version: 0,
      });
    }
  },

  setCurrentBox: (box) => set({ currentBox: box }),
  setStatus: (msg) => set({ statusMessage: msg }),
  setClipboard: (raw, name) => set({ clipboardRaw: raw, clipboardName: name }),
  clearClipboard: () => set({ clipboardRaw: null, clipboardName: null }),
  notifyChange: () => set((state) => ({ version: state.version + 1 })),
}));
