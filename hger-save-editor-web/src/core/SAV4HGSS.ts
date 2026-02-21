import * as PokeCrypto from './PokeCrypto';
import { PK4 } from './PK4';
import { decodeString, encodeString } from './StringConverter4';

// ==================== 세이브 구조 상수 ====================

const SAVE_SIZE = 0x80000;

const GENERAL_A_BASE = 0x00000;
const STORAGE_A_BASE = 0x10000;
const GENERAL_B_BASE = 0x40000;
const STORAGE_B_BASE = 0x50000;

const GENERAL_ALLOC_SIZE = 0x10000;
const STORAGE_ALLOC_SIZE = 0x30000;

const FOOTER_SIZE = 0x10;
const FOOTER_OFS_SAVE_COUNT = 0x00;
const FOOTER_OFS_CRC = 0x0E;

const OFS_TRAINER_NAME = 0x64;
const OFS_TID = 0x74;
const OFS_SID = 0x76;
const OFS_MONEY = 0x78;
const OFS_GENDER = 0x7C;
const OFS_PLAYTIME_H = 0x86;
const OFS_PLAYTIME_M = 0x88;
const OFS_PLAYTIME_S = 0x89;
const OFS_PARTY_COUNT = 0x94;
const OFS_PARTY = 0x98;

const OFS_BOX_DATA = 0x0000;
const BOX_ALLOC_SIZE = 0x1000;
const OFS_BOX_NAMES = 0x1E000;
const BOX_NAME_ENTRY_SIZE = 40;
const BOX_NAME_HEADER_SIZE = 12;
const OFS_CURRENT_BOX = 0x1E4B0;

export const BOX_COUNT = 30;
export const BOX_SLOT_COUNT = 30;

// ==================== SAV4HGSS ====================

export class SAV4HGSS {
  private readonly raw: Uint8Array;
  private readonly view: DataView;
  private generalBase: number = 0;
  private storageBase: number = 0;
  private generalBlockSize: number = 0;
  private storageBlockSize: number = 0;

  isModified: boolean = false;
  fileName: string | null;

  private constructor(data: Uint8Array, fileName: string | null) {
    this.raw = data;
    this.view = new DataView(data.buffer, data.byteOffset, data.byteLength);
    this.fileName = fileName;
    this.detectActiveBlocks();
  }

  static loadFromArrayBuffer(buffer: ArrayBuffer, fileName: string): SAV4HGSS | null {
    let data = new Uint8Array(buffer);
    if (data.length < SAVE_SIZE) {
      const padded = new Uint8Array(SAVE_SIZE);
      padded.set(data);
      data = padded;
    }
    return new SAV4HGSS(data, fileName);
  }

  // ==================== 블록 감지 ====================

  private detectActiveBlocks(): void {
    const genSizeA = this.detectBlockSize(GENERAL_A_BASE, GENERAL_ALLOC_SIZE);
    const genSizeB = this.detectBlockSize(GENERAL_B_BASE, GENERAL_ALLOC_SIZE);

    const cntA = genSizeA > 0 ? this.getSaveCount(GENERAL_A_BASE, genSizeA) : 0;
    const cntB = genSizeB > 0 ? this.getSaveCount(GENERAL_B_BASE, genSizeB) : 0;

    if (cntA >= cntB && genSizeA > 0) {
      this.generalBase = GENERAL_A_BASE;
      this.storageBase = STORAGE_A_BASE;
      this.generalBlockSize = genSizeA;
    } else if (genSizeB > 0) {
      this.generalBase = GENERAL_B_BASE;
      this.storageBase = STORAGE_B_BASE;
      this.generalBlockSize = genSizeB;
    } else {
      this.generalBase = GENERAL_A_BASE;
      this.storageBase = STORAGE_A_BASE;
      this.generalBlockSize = genSizeA > 0 ? genSizeA : 0xF628;
    }

    this.storageBlockSize = this.detectBlockSize(this.storageBase, STORAGE_ALLOC_SIZE);
    if (this.storageBlockSize <= 0) {
      this.storageBlockSize = 0x12310;
    }
  }

  private detectBlockSize(blockBase: number, allocSize: number): number {
    const maxSize = Math.min(allocSize, this.raw.length - blockBase);
    for (let bs = FOOTER_SIZE + 4; bs <= maxSize; bs += 4) {
      const sizeFieldOfs = blockBase + bs - 12;
      if (sizeFieldOfs + 4 > this.raw.length) continue;
      const storedSize = this.view.getUint32(sizeFieldOfs, true);
      if (storedSize !== bs) continue;

      const footerOfs = blockBase + bs - FOOTER_SIZE;
      const crcOfs = footerOfs + FOOTER_OFS_CRC;
      if (crcOfs + 2 > this.raw.length) continue;

      const storedCRC = this.view.getUint16(crcOfs, true);
      const dataSize = bs - FOOTER_SIZE;
      const computedCRC = PokeCrypto.crc16(this.raw, blockBase, dataSize);
      if (computedCRC === storedCRC) return bs;
    }
    return -1;
  }

  private getSaveCount(blockBase: number, blockSize: number): number {
    const footerOfs = blockBase + blockSize - FOOTER_SIZE;
    if (footerOfs + 4 > this.raw.length) return 0;
    return this.view.getUint32(footerOfs + FOOTER_OFS_SAVE_COUNT, true);
  }

  // ==================== 트레이너 정보 ====================

  get trainerName(): string {
    return decodeString(this.raw, this.generalBase + OFS_TRAINER_NAME, 8);
  }
  set trainerName(value: string) {
    encodeString(value, this.raw, this.generalBase + OFS_TRAINER_NAME, 8);
    this.isModified = true;
  }

  get tid(): number { return this.view.getUint16(this.generalBase + OFS_TID, true); }
  set tid(value: number) { this.view.setUint16(this.generalBase + OFS_TID, value, true); this.isModified = true; }

  get sid(): number { return this.view.getUint16(this.generalBase + OFS_SID, true); }
  set sid(value: number) { this.view.setUint16(this.generalBase + OFS_SID, value, true); this.isModified = true; }

  get money(): number { return this.view.getUint32(this.generalBase + OFS_MONEY, true); }
  set money(value: number) { this.view.setUint32(this.generalBase + OFS_MONEY, Math.min(value, 999999), true); this.isModified = true; }

  get gender(): number { return this.raw[this.generalBase + OFS_GENDER]!; }
  set gender(value: number) { this.raw[this.generalBase + OFS_GENDER] = value & 1; this.isModified = true; }

  get playtime(): { hours: number; minutes: number; seconds: number } {
    return {
      hours: this.view.getUint16(this.generalBase + OFS_PLAYTIME_H, true),
      minutes: this.raw[this.generalBase + OFS_PLAYTIME_M]!,
      seconds: this.raw[this.generalBase + OFS_PLAYTIME_S]!,
    };
  }

  // ==================== 파티 포켓몬 ====================

  get partyCount(): number { return this.raw[this.generalBase + OFS_PARTY_COUNT]!; }
  private set partyCount(value: number) {
    this.raw[this.generalBase + OFS_PARTY_COUNT] = Math.max(0, Math.min(6, value));
  }

  getPartySlot(index: number): PK4 {
    if (index < 0 || index > 5) return PK4.empty;
    const offset = this.generalBase + OFS_PARTY + index * PokeCrypto.SIZE_4PARTY;
    const raw = this.raw.slice(offset, offset + PokeCrypto.SIZE_4PARTY);
    return new PK4(raw);
  }

  setPartySlot(index: number, pk: PK4): void {
    if (index < 0 || index > 5) return;
    pk.refreshStats();
    const encrypted = pk.writeToRaw();
    const offset = this.generalBase + OFS_PARTY + index * PokeCrypto.SIZE_4PARTY;
    this.raw.set(encrypted, offset);

    const currentCount = this.partyCount;
    if (pk.species === 0) {
      if (index < currentCount) {
        let newCount = 0;
        for (let i = 0; i < currentCount; i++) {
          if (i === index) continue;
          const slotOfs = this.generalBase + OFS_PARTY + i * PokeCrypto.SIZE_4PARTY;
          const dec = PokeCrypto.decryptPK4(this.raw.slice(slotOfs, slotOfs + PokeCrypto.SIZE_4PARTY));
          const speciesRaw = new DataView(dec.buffer, dec.byteOffset, dec.byteLength).getUint16(8, true);
          if (speciesRaw !== 0) newCount = i + 1;
        }
        this.partyCount = newCount;
      }
    } else if (index + 1 > currentCount) {
      this.partyCount = index + 1;
    }

    this.isModified = true;
  }

  // ==================== 박스 포켓몬 ====================

  getBoxSlot(box: number, slot: number): PK4 {
    const offset = this.storageBase + OFS_BOX_DATA
                 + box * BOX_ALLOC_SIZE
                 + slot * PokeCrypto.SIZE_4STORED;
    const raw = this.raw.slice(offset, offset + PokeCrypto.SIZE_4STORED);
    return new PK4(raw);
  }

  setBoxSlot(box: number, slot: number, pk: PK4): void {
    const stored = PokeCrypto.convertToStored(pk.writeToRaw());
    const offset = this.storageBase + OFS_BOX_DATA
                 + box * BOX_ALLOC_SIZE
                 + slot * PokeCrypto.SIZE_4STORED;
    this.raw.set(stored, offset);
    this.isModified = true;
  }

  getBoxName(box: number): string {
    const offset = this.storageBase + OFS_BOX_NAMES + box * BOX_NAME_ENTRY_SIZE + BOX_NAME_HEADER_SIZE;
    if (offset + (BOX_NAME_ENTRY_SIZE - BOX_NAME_HEADER_SIZE) > this.raw.length) return `BOX ${box + 1}`;
    return decodeString(this.raw, offset, 14);
  }

  setBoxName(box: number, name: string): void {
    const offset = this.storageBase + OFS_BOX_NAMES + box * BOX_NAME_ENTRY_SIZE + BOX_NAME_HEADER_SIZE;
    if (offset + (BOX_NAME_ENTRY_SIZE - BOX_NAME_HEADER_SIZE) > this.raw.length) return;
    encodeString(name, this.raw, offset, 14);
    this.isModified = true;
  }

  get currentBox(): number {
    const offset = this.storageBase + OFS_CURRENT_BOX;
    return offset < this.raw.length ? this.raw[offset]! : 0;
  }

  // ==================== 저장 ====================

  saveToBlob(): Blob {
    this.recalcBlockCRC(this.generalBase, this.generalBlockSize);
    this.recalcBlockCRC(this.storageBase, this.storageBlockSize);
    this.isModified = false;
    return new Blob([new Uint8Array(this.raw)], { type: 'application/octet-stream' });
  }

  private recalcBlockCRC(blockBase: number, blockSize: number): void {
    if (blockSize <= FOOTER_SIZE) return;
    const dataSize = blockSize - FOOTER_SIZE;
    const crcOfs = blockBase + blockSize - 2;
    if (crcOfs + 2 > this.raw.length) return;

    const crc = PokeCrypto.crc16(this.raw, blockBase, dataSize);
    this.view.setUint16(crcOfs, crc, true);
  }

  // ==================== 진단 ====================

  getBlockInfo(): string {
    const cnt = this.generalBlockSize > 0
      ? this.getSaveCount(this.generalBase, this.generalBlockSize)
      : 0;
    const active = this.generalBase === GENERAL_A_BASE ? 'A' : 'B';
    return `Active=${active}  General=0x${this.generalBlockSize.toString(16).toUpperCase()}  Storage=0x${this.storageBlockSize.toString(16).toUpperCase()}  SaveCount=${cnt}`;
  }
}
