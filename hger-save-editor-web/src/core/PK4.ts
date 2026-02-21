import * as PokeCrypto from './PokeCrypto';
import * as GameData from './GameData';
import { decodeString, encodeString } from './StringConverter4';

const SPECIES_DISPLAY_THRESHOLD = 494;
const SPECIES_STORED_THRESHOLD = 544;
const SPECIES_STORED_OFFSET = 50;

export class PK4 {
  private readonly data: Uint8Array;
  private readonly view: DataView;
  private readonly skipChecksumValidation: boolean;

  get isPartyForm(): boolean { return this.data.length === PokeCrypto.SIZE_4PARTY; }

  constructor(rawEncrypted: Uint8Array) {
    this.data = PokeCrypto.decryptPK4(rawEncrypted);
    this.view = new DataView(this.data.buffer, this.data.byteOffset, this.data.byteLength);
    this.skipChecksumValidation = false;
  }

  private static fromDecrypted(data: Uint8Array, skipChecksum: boolean): PK4 {
    const pk = Object.create(PK4.prototype) as PK4;
    (pk as unknown as { data: Uint8Array }).data = data;
    (pk as unknown as { view: DataView }).view = new DataView(data.buffer, data.byteOffset, data.byteLength);
    (pk as unknown as { skipChecksumValidation: boolean }).skipChecksumValidation = skipChecksum;
    return pk;
  }

  static get empty(): PK4 {
    return PK4.fromDecrypted(new Uint8Array(PokeCrypto.SIZE_4STORED), true);
  }

  static createBlank(dataSize: number): PK4 {
    return PK4.fromDecrypted(new Uint8Array(dataSize), true);
  }

  clone(): PK4 {
    return PK4.fromDecrypted(new Uint8Array(this.data), this.skipChecksumValidation);
  }

  get isEmpty(): boolean { return this.species === 0 || !this.isChecksumValid; }

  private get isChecksumValid(): boolean {
    if (this.skipChecksumValidation) return true;
    const stored = this.view.getUint16(6, true);
    return PokeCrypto.calcChecksum(this.data) === stored;
  }

  // ==================== Block A (0x08-0x27) ====================

  get species(): number {
    const raw = this.view.getUint16(0x08, true);
    return raw >= SPECIES_STORED_THRESHOLD ? raw - SPECIES_STORED_OFFSET : raw;
  }
  set species(value: number) {
    const stored = value >= SPECIES_DISPLAY_THRESHOLD ? value + SPECIES_STORED_OFFSET : value;
    this.view.setUint16(0x08, stored, true);
  }

  get heldItem(): number { return this.view.getUint16(0x0A, true); }
  set heldItem(value: number) { this.view.setUint16(0x0A, value, true); }

  get tid(): number { return this.view.getUint16(0x0C, true); }
  set tid(value: number) { this.view.setUint16(0x0C, value, true); }

  get sid(): number { return this.view.getUint16(0x0E, true); }
  set sid(value: number) { this.view.setUint16(0x0E, value, true); }

  get exp(): number { return this.view.getUint32(0x10, true); }
  set exp(value: number) { this.view.setUint32(0x10, value, true); }

  get friendship(): number { return this.data[0x14]!; }
  set friendship(value: number) { this.data[0x14] = value; }

  get ability(): number { return this.data[0x15]!; }
  set ability(value: number) { this.data[0x15] = value; }

  get markings(): number { return this.data[0x16]!; }
  set markings(value: number) { this.data[0x16] = value; }

  get language(): number { return this.data[0x17]!; }
  set language(value: number) { this.data[0x17] = value; }

  get evHP(): number  { return this.data[0x18]!; } set evHP(v: number)  { this.data[0x18] = v; }
  get evAtk(): number { return this.data[0x19]!; } set evAtk(v: number) { this.data[0x19] = v; }
  get evDef(): number { return this.data[0x1A]!; } set evDef(v: number) { this.data[0x1A] = v; }
  get evSpe(): number { return this.data[0x1B]!; } set evSpe(v: number) { this.data[0x1B] = v; }
  get evSpA(): number { return this.data[0x1C]!; } set evSpA(v: number) { this.data[0x1C] = v; }
  get evSpD(): number { return this.data[0x1D]!; } set evSpD(v: number) { this.data[0x1D] = v; }

  // ==================== Block B (0x28-0x47) ====================

  get form(): number { return this.data[0x40]! >> 3; }
  set form(value: number) { this.data[0x40] = (this.data[0x40]! & 0x07) | ((value & 0x1F) << 3); }

  get move1(): number { return this.view.getUint16(0x28, true); }
  set move1(v: number) { this.view.setUint16(0x28, v, true); }
  get move2(): number { return this.view.getUint16(0x2A, true); }
  set move2(v: number) { this.view.setUint16(0x2A, v, true); }
  get move3(): number { return this.view.getUint16(0x2C, true); }
  set move3(v: number) { this.view.setUint16(0x2C, v, true); }
  get move4(): number { return this.view.getUint16(0x2E, true); }
  set move4(v: number) { this.view.setUint16(0x2E, v, true); }

  get pp1(): number { return this.data[0x30]!; } set pp1(v: number) { this.data[0x30] = v; }
  get pp2(): number { return this.data[0x31]!; } set pp2(v: number) { this.data[0x31] = v; }
  get pp3(): number { return this.data[0x32]!; } set pp3(v: number) { this.data[0x32] = v; }
  get pp4(): number { return this.data[0x33]!; } set pp4(v: number) { this.data[0x33] = v; }

  get ppUp1(): number { return this.data[0x34]!; } set ppUp1(v: number) { this.data[0x34] = v; }
  get ppUp2(): number { return this.data[0x35]!; } set ppUp2(v: number) { this.data[0x35] = v; }
  get ppUp3(): number { return this.data[0x36]!; } set ppUp3(v: number) { this.data[0x36] = v; }
  get ppUp4(): number { return this.data[0x37]!; } set ppUp4(v: number) { this.data[0x37] = v; }

  // IVs: bit-packed uint32 @ 0x38
  private get ivData(): number { return this.view.getUint32(0x38, true); }
  private set ivData(value: number) { this.view.setUint32(0x38, value >>> 0, true); }

  get ivHP(): number  { return (this.ivData >>>  0) & 0x1F; }
  set ivHP(v: number)  { this.ivData = ((this.ivData & ~(0x1F <<  0)) | ((v & 0x1F) <<  0)) >>> 0; }
  get ivAtk(): number { return (this.ivData >>>  5) & 0x1F; }
  set ivAtk(v: number) { this.ivData = ((this.ivData & ~(0x1F <<  5)) | ((v & 0x1F) <<  5)) >>> 0; }
  get ivDef(): number { return (this.ivData >>> 10) & 0x1F; }
  set ivDef(v: number) { this.ivData = ((this.ivData & ~(0x1F << 10)) | ((v & 0x1F) << 10)) >>> 0; }
  get ivSpe(): number { return (this.ivData >>> 15) & 0x1F; }
  set ivSpe(v: number) { this.ivData = ((this.ivData & ~(0x1F << 15)) | ((v & 0x1F) << 15)) >>> 0; }
  get ivSpA(): number { return (this.ivData >>> 20) & 0x1F; }
  set ivSpA(v: number) { this.ivData = ((this.ivData & ~(0x1F << 20)) | ((v & 0x1F) << 20)) >>> 0; }
  get ivSpD(): number { return (this.ivData >>> 25) & 0x1F; }
  set ivSpD(v: number) { this.ivData = ((this.ivData & ~(0x1F << 25)) | ((v & 0x1F) << 25)) >>> 0; }

  get isEgg(): boolean { return (this.ivData & (1 << 30)) !== 0; }
  set isEgg(v: boolean) { this.ivData = v ? (this.ivData | (1 << 30)) >>> 0 : (this.ivData & ~(1 << 30)) >>> 0; }

  get isNicknamed(): boolean { return (this.ivData & (1 << 31)) !== 0; }
  set isNicknamed(v: boolean) { this.ivData = v ? (this.ivData | (1 << 31)) >>> 0 : (this.ivData & ~(1 << 31)) >>> 0; }

  get statNature(): number { return this.data[0x42]!; }
  set statNature(value: number) { this.data[0x42] = value; }

  get effectiveNature(): number {
    const raw = this.statNature;
    if (raw >= 2 && raw % 2 === 0) {
      const idx = raw / 2 - 1;
      if (idx <= 24) return idx;
    }
    return this.nature;
  }

  get metLocation(): number { return this.view.getUint16(0x46, true); }
  set metLocation(v: number) { this.view.setUint16(0x46, v, true); }

  // ==================== Block C (0x48-0x67) ====================

  get nickname(): string { return decodeString(this.data, 0x48, 11); }
  set nickname(value: string) { encodeString(value, this.data, 0x48, 11); }

  get originGame(): number { return this.data[0x5F]!; }
  set originGame(value: number) { this.data[0x5F] = value; }

  // ==================== Block D (0x68-0x87) ====================

  get otName(): string { return decodeString(this.data, 0x68, 8); }
  set otName(value: string) { encodeString(value, this.data, 0x68, 8); }

  get eggDate(): { year: number; month: number; day: number } {
    return { year: this.data[0x78]!, month: this.data[0x79]!, day: this.data[0x7A]! };
  }
  set eggDate(v: { year: number; month: number; day: number }) {
    this.data[0x78] = v.year; this.data[0x79] = v.month; this.data[0x7A] = v.day;
  }

  get metDate(): { year: number; month: number; day: number } {
    return { year: this.data[0x7B]!, month: this.data[0x7C]!, day: this.data[0x7D]! };
  }
  set metDate(v: { year: number; month: number; day: number }) {
    this.data[0x7B] = v.year; this.data[0x7C] = v.month; this.data[0x7D] = v.day;
  }

  get ball(): number { return this.data[0x86]!; }
  set ball(value: number) { this.data[0x86] = value; this.data[0x83] = value; }

  private get metLevelGender(): number { return this.data[0x84]!; }
  private set metLevelGender(value: number) { this.data[0x84] = value; }

  get metLevel(): number { return this.metLevelGender & 0x7F; }
  set metLevel(value: number) { this.metLevelGender = (this.metLevelGender & 0x80) | (value & 0x7F); }

  get otIsFemale(): boolean { return (this.metLevelGender & 0x80) !== 0; }
  set otIsFemale(value: boolean) { this.metLevelGender = (this.metLevelGender & 0x7F) | (value ? 0x80 : 0); }

  // ==================== 파생 속성 ====================

  get pid(): number { return this.view.getUint32(0x00, true); }
  set pid(value: number) { this.view.setUint32(0x00, value >>> 0, true); }

  get gender(): number {
    const threshold = GameData.getGenderRatio(this.species);
    if (threshold === 255) return 2;
    if (threshold === 254) return 1;
    if (threshold === 0) return 0;
    return (this.pid & 0xFF) < threshold ? 1 : 0;
  }

  get nature(): number { return this.pid % 25; }

  get isShiny(): boolean {
    const pid = this.pid;
    return (this.tid ^ this.sid ^ ((pid >>> 16) & 0xFFFF) ^ (pid & 0xFFFF)) < 8;
  }

  get level(): number {
    const exp = this.exp;
    const growthRate = GameData.getGrowthRate(this.species);
    for (let lv = 100; lv >= 1; lv--) {
      if (exp >= GameData.getExpTable(growthRate, lv)) return lv;
    }
    return 1;
  }
  set level(value: number) {
    this.exp = GameData.calcExpForLevel(value, this.species);
  }

  // ==================== 배틀 스탯 (파티 전용) ====================

  get status(): number { return this.isPartyForm ? this.view.getUint32(0x88, true) : 0; }
  set status(v: number) { if (this.isPartyForm) this.view.setUint32(0x88, v, true); }

  get currentLevel(): number { return this.isPartyForm ? this.data[0x8C]! : 0; }
  set currentLevel(v: number) { if (this.isPartyForm) this.data[0x8C] = v; }

  get currentHP(): number { return this.isPartyForm ? this.view.getUint16(0x8E, true) : 0; }
  set currentHP(v: number) { if (this.isPartyForm) this.view.setUint16(0x8E, v, true); }

  get maxHP(): number { return this.isPartyForm ? this.view.getUint16(0x90, true) : 0; }
  set maxHP(v: number) { if (this.isPartyForm) this.view.setUint16(0x90, v, true); }

  get statAtk(): number { return this.isPartyForm ? this.view.getUint16(0x92, true) : 0; }
  set statAtk(v: number) { if (this.isPartyForm) this.view.setUint16(0x92, v, true); }

  get statDef(): number { return this.isPartyForm ? this.view.getUint16(0x94, true) : 0; }
  set statDef(v: number) { if (this.isPartyForm) this.view.setUint16(0x94, v, true); }

  get statSpe(): number { return this.isPartyForm ? this.view.getUint16(0x96, true) : 0; }
  set statSpe(v: number) { if (this.isPartyForm) this.view.setUint16(0x96, v, true); }

  get statSpA(): number { return this.isPartyForm ? this.view.getUint16(0x98, true) : 0; }
  set statSpA(v: number) { if (this.isPartyForm) this.view.setUint16(0x98, v, true); }

  get statSpD(): number { return this.isPartyForm ? this.view.getUint16(0x9A, true) : 0; }
  set statSpD(v: number) { if (this.isPartyForm) this.view.setUint16(0x9A, v, true); }

  // ==================== 직렬화 ====================

  writeToRaw(): Uint8Array {
    return PokeCrypto.encryptPK4(new Uint8Array(this.data));
  }

  refreshStats(): void {
    if (!this.isPartyForm) return;
    const lv = this.level;
    this.currentLevel = lv;
    const bs = GameData.getBaseStats(this.species, this.form);
    if (!bs) return;

    this.maxHP  = calcStat(bs.hp,  this.ivHP,  this.evHP,  lv, 1.0, true);
    this.statAtk = calcStat(bs.atk, this.ivAtk, this.evAtk, lv, this.natureModifier(0));
    this.statDef = calcStat(bs.def, this.ivDef, this.evDef, lv, this.natureModifier(1));
    this.statSpA = calcStat(bs.spa, this.ivSpA, this.evSpA, lv, this.natureModifier(3));
    this.statSpD = calcStat(bs.spd, this.ivSpD, this.evSpD, lv, this.natureModifier(4));
    this.statSpe = calcStat(bs.spe, this.ivSpe, this.evSpe, lv, this.natureModifier(2));
    this.currentHP = this.maxHP;
  }

  // ==================== 유효성 검사 ====================

  getValidationWarnings(): string[] {
    const warnings: string[] = [];
    if (this.species === 0) warnings.push("종류가 '없음'입니다.");
    if (!this.nickname.trim()) warnings.push('닉네임이 없습니다.');
    if (this.ability === 0) warnings.push('특성이 없습니다.');
    const hasMove =
      (this.move1 !== 0 && this.move1 !== 0xFFFF) ||
      (this.move2 !== 0 && this.move2 !== 0xFFFF) ||
      (this.move3 !== 0 && this.move3 !== 0xFFFF) ||
      (this.move4 !== 0 && this.move4 !== 0xFFFF);
    if (!hasMove) warnings.push('배운 기술이 하나도 없습니다.');
    if (this.metLevel === 0) warnings.push('만난 레벨이 0입니다.');
    return warnings;
  }

  get hasWarnings(): boolean {
    return !this.isEmpty && this.getValidationWarnings().length > 0;
  }

  // ==================== 내부 헬퍼 ====================

  private natureModifier(statIndex: number): number {
    const nat = this.effectiveNature;
    const boost = Math.floor(nat / 5);
    const reduce = nat % 5;
    if (boost === reduce) return 1.0;
    if (boost === statIndex) return 1.1;
    if (reduce === statIndex) return 0.9;
    return 1.0;
  }
}

function calcStat(baseStat: number, iv: number, ev: number, level: number, natureModifier: number = 1.0, isHP: boolean = false): number {
  const val = Math.floor((2 * baseStat + iv + Math.floor(ev / 4)) * level / 100);
  if (isHP) return val + level + 10;
  return Math.floor((val + 5) * natureModifier);
}
