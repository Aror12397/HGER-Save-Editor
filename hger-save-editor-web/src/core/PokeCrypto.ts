export const SIZE_4PARTY = 236;
export const SIZE_4STORED = 136;
const BLOCK_SIZE = 32;

const BLOCK_ORDER_TABLE: readonly (readonly number[])[] = [
  [0,1,2,3],[0,1,3,2],[0,2,1,3],[0,2,3,1],[0,3,1,2],[0,3,2,1],
  [1,0,2,3],[1,0,3,2],[1,2,0,3],[1,2,3,0],[1,3,0,2],[1,3,2,0],
  [2,0,1,3],[2,0,3,1],[2,1,0,3],[2,1,3,0],[2,3,0,1],[2,3,1,0],
  [3,0,1,2],[3,0,2,1],[3,1,0,2],[3,1,2,0],[3,2,0,1],[3,2,1,0],
];

function getShuffleValue(pid: number): number {
  return (((pid & 0x3E000) >>> 0xD) % 24);
}

function cryptSubstructure(data: Uint8Array, seed: number, offset: number, length: number): void {
  let s = seed >>> 0;
  for (let i = 0; i < length; i += 2) {
    s = (Math.imul(s, 0x41C64E6D) + 0x6073) >>> 0;
    data[offset + i]     ^= (s >>> 16) & 0xFF;
    data[offset + i + 1] ^= (s >>> 24) & 0xFF;
  }
}

function shuffleBlocks(data: Uint8Array, sv: number): void {
  const order = BLOCK_ORDER_TABLE[sv]!;
  const temp = new Uint8Array(BLOCK_SIZE * 4);
  for (let i = 0; i < 4; i++) {
    temp.set(data.subarray(8 + order[i]! * BLOCK_SIZE, 8 + order[i]! * BLOCK_SIZE + BLOCK_SIZE), i * BLOCK_SIZE);
  }
  data.set(temp, 8);
}

function unshuffleBlocks(data: Uint8Array, sv: number): void {
  const order = BLOCK_ORDER_TABLE[sv]!;
  const temp = new Uint8Array(BLOCK_SIZE * 4);
  for (let i = 0; i < 4; i++) {
    temp.set(data.subarray(8 + i * BLOCK_SIZE, 8 + i * BLOCK_SIZE + BLOCK_SIZE), order[i]! * BLOCK_SIZE);
  }
  data.set(temp, 8);
}

export function decryptPK4(data: Uint8Array): Uint8Array {
  const result = new Uint8Array(data);
  const view = new DataView(data.buffer, data.byteOffset, data.byteLength);
  const seed = view.getUint16(6, true);

  cryptSubstructure(result, seed, 8, 128);

  const pid = view.getUint32(0, true);
  const sv = getShuffleValue(pid);
  unshuffleBlocks(result, sv);

  if (result.length === SIZE_4PARTY) {
    cryptSubstructure(result, pid, 0x88, SIZE_4PARTY - 0x88);
  }

  return result;
}

export function encryptPK4(decrypted: Uint8Array): Uint8Array {
  const result = new Uint8Array(decrypted);
  const view = new DataView(result.buffer, result.byteOffset, result.byteLength);

  const pid = view.getUint32(0, true);
  const sv = getShuffleValue(pid);

  const checksum = calcChecksum(decrypted);
  view.setUint16(6, checksum, true);

  shuffleBlocks(result, sv);

  cryptSubstructure(result, checksum, 8, 128);

  if (result.length === SIZE_4PARTY) {
    cryptSubstructure(result, pid, 0x88, SIZE_4PARTY - 0x88);
  }

  return result;
}

export function convertToParty(stored: Uint8Array): Uint8Array {
  const party = new Uint8Array(SIZE_4PARTY);
  party.set(stored.subarray(0, SIZE_4STORED));
  return party;
}

export function convertToStored(party: Uint8Array): Uint8Array {
  const stored = new Uint8Array(SIZE_4STORED);
  stored.set(party.subarray(0, SIZE_4STORED));
  return stored;
}

export function calcChecksum(decrypted: Uint8Array): number {
  const view = new DataView(decrypted.buffer, decrypted.byteOffset, decrypted.byteLength);
  let sum = 0;
  for (let i = 8; i < 136; i += 2) {
    sum = (sum + view.getUint16(i, true)) & 0xFFFFFFFF;
  }
  return sum & 0xFFFF;
}

export function crc16(data: Uint8Array, offset: number, length: number): number {
  let crc = 0xFFFF;
  for (let i = offset; i < offset + length; i++) {
    crc ^= (data[i]! << 8) & 0xFFFF;
    for (let j = 0; j < 8; j++) {
      if ((crc & 0x8000) !== 0) {
        crc = ((crc << 1) ^ 0x1021) & 0xFFFF;
      } else {
        crc = (crc << 1) & 0xFFFF;
      }
    }
  }
  return crc & 0xFFFF;
}
