const TERMINATOR = 0xFFFF;

let decodeTable = new Map<number, string>();
let encodeTable = new Map<string, number>();

export function initializeCharmap(charmapText: string): void {
  decodeTable.clear();
  encodeTable.clear();

  for (const rawLine of charmapText.split('\n')) {
    const commentIdx = rawLine.indexOf('//');
    let line = commentIdx >= 0 ? rawLine.substring(0, commentIdx) : rawLine;
    line = line.replace(/^[ \t]+|[\r\n]+$/g, '');
    if (line.length === 0) continue;

    const eqIdx = line.indexOf('=');
    if (eqIdx < 0) continue;

    const codeStr = line.substring(0, eqIdx);
    const val = line.substring(eqIdx + 1);

    const code = parseInt(codeStr, 16);
    if (isNaN(code) || code < 0 || code > 0xFFFF) continue;

    if (val.startsWith('{')) continue;
    if (val === '\\x0000') continue;
    if (val.length !== 1) continue;

    const c = val;
    if (c === '\0') continue;

    decodeTable.set(code, c);
    if (!encodeTable.has(c)) {
      encodeTable.set(c, code);
    }
  }
}

export function decodeString(data: Uint8Array, offset: number, maxChars: number): string {
  const view = new DataView(data.buffer, data.byteOffset, data.byteLength);
  let result = '';
  for (let i = 0; i < maxChars; i++) {
    const code = view.getUint16(offset + i * 2, true);
    if (code === TERMINATOR || code === 0x0000) break;

    const c = decodeTable.get(code);
    if (c !== undefined) {
      result += c;
    } else {
      break;
    }
  }
  return result;
}

export function encodeString(text: string, data: Uint8Array, offset: number, maxChars: number): void {
  const view = new DataView(data.buffer, data.byteOffset, data.byteLength);

  for (let i = 0; i < maxChars; i++) {
    view.setUint16(offset + i * 2, TERMINATOR, true);
  }

  const len = Math.min(text.length, maxChars - 1);
  for (let i = 0; i < len; i++) {
    const code = encodeTable.get(text[i]) ?? 0x0000;
    view.setUint16(offset + i * 2, code, true);
  }
  view.setUint16(offset + len * 2, TERMINATOR, true);
}
