import { initializeCharmap } from './StringConverter4';

// ==================== 타입 ====================

export interface BaseStatsRecord {
  hp: number; atk: number; def: number;
  spa: number; spd: number; spe: number;
}

// ==================== 내부 데이터 ====================

let speciesNames: string[] = [];
let moveNames: string[] = [];
let itemNames: string[] = [];
let abilityNames: string[] = [];
let ballNames: string[] = [];
const locationNames = new Map<number, string>();
const formNames = new Map<string, string>();
const baseStats = new Map<string, BaseStatsRecord>();
const growthRateTable = new Int32Array(2000);
const genderRatioTable = new Uint8Array(2000).fill(127);
let spriteDir = '';

// ==================== 성격 ====================

export const NatureNames: readonly string[] = [
  '노력',   '외로움', '용감',     '고집',     '개구쟁이',
  '대담',   '온순',   '무사태평', '장난꾸러기', '촐랑',
  '겁쟁이', '성급',   '성실',     '명랑',     '천진난만',
  '조심',   '의젓',   '냉정',     '수줍음',   '덜렁',
  '차분',   '얌전',   '건방',     '신중',     '변덕',
];

const NatureTable: number[][] = [];
for (let i = 0; i < 25; i++) {
  NatureTable[i] = [1.0, 1.0, 1.0, 1.0, 1.0];
  const boost = Math.floor(i / 5);
  const reduce = i % 5;
  if (boost !== reduce) {
    NatureTable[i]![boost] = 1.1;
    NatureTable[i]![reduce] = 0.9;
  }
}

export function getNatureModifier(nature: number, statIndex: number): number {
  if (nature >= 0 && nature < 25) return NatureTable[nature]![statIndex]!;
  return 1.0;
}

// ==================== 타입 이름 ====================

export const TypeNames: readonly string[] = [
  '노말', '불꽃', '물', '전기', '풀', '얼음', '격투', '독',
  '땅', '비행', '에스퍼', '벌레', '바위', '고스트', '드래곤',
  '악', '강철', '???',
];

// ==================== 초기화 ====================

async function fetchText(url: string): Promise<string> {
  try {
    const res = await fetch(url);
    if (!res.ok) return '';
    return await res.text();
  } catch { return ''; }
}

function loadLines(text: string): string[] {
  if (!text) return [];
  return text.split('\n').map(l => l.replace(/\r$/, ''));
}

function parseLocations(text: string): void {
  locationNames.clear();
  for (const line of text.split('\n')) {
    const sep = line.indexOf('\t');
    if (sep < 0) continue;
    const id = parseInt(line.substring(0, sep), 10);
    if (isNaN(id)) continue;
    locationNames.set(id, line.substring(sep + 1).replace(/\r$/, ''));
  }
}

function parseGenderless(text: string): void {
  for (const line of text.split('\n')) {
    const id = parseInt(line.trim(), 10);
    if (!isNaN(id) && id > 0 && id < genderRatioTable.length) {
      genderRatioTable[id] = 255;
    }
  }
}

function parseGrowthRates(text: string): void {
  for (const line of text.split('\n')) {
    const sep = line.indexOf(',');
    if (sep < 0) continue;
    const id = parseInt(line.substring(0, sep), 10);
    if (isNaN(id) || id <= 0 || id >= growthRateTable.length) continue;
    growthRateTable[id] = parseGrowthRateName(line.substring(sep + 1).trim());
  }
}

function parseGrowthRateName(name: string): number {
  switch (name) {
    case 'Medium Fast':  return 0;
    case 'Erratic':      return 1;
    case 'Fluctuating':  return 2;
    case 'Medium Slow':  return 3;
    case 'Fast':         return 4;
    case 'Slow':         return 5;
    default:             return 0;
  }
}

function parseBaseStats(text: string): void {
  buildDefaultBaseStats();
  for (const line of text.split('\n')) {
    const parts = line.split(',');
    if (parts.length < 7) continue;
    const id = parseInt(parts[0]!, 10);
    if (isNaN(id) || id <= 0) continue;

    let form: number, hp: number, atk: number, def: number, spa: number, spd: number, spe: number;
    if (parts.length >= 8 && !isNaN(parseInt(parts[1]!, 10))) {
      form = parseInt(parts[1]!, 10);
      hp  = parseInt(parts[2]!, 10); atk = parseInt(parts[3]!, 10);
      def = parseInt(parts[4]!, 10); spa = parseInt(parts[5]!, 10);
      spd = parseInt(parts[6]!, 10); spe = parseInt(parts[7]!, 10);
    } else {
      form = 0;
      hp  = parseInt(parts[1]!, 10); atk = parseInt(parts[2]!, 10);
      def = parseInt(parts[3]!, 10); spa = parseInt(parts[4]!, 10);
      spd = parseInt(parts[5]!, 10); spe = parseInt(parts[6]!, 10);
    }

    if ([hp, atk, def, spa, spd, spe].some(v => isNaN(v))) continue;
    baseStats.set(`${id}_${form}`, { hp, atk, def, spa, spd, spe });
  }
}

function parseFormNames(text: string): void {
  formNames.clear();
  for (const line of text.split('\n')) {
    const parts = line.split('\t');
    if (parts.length < 3) continue;
    const species = parseInt(parts[0]!, 10);
    const form = parseInt(parts[1]!, 10);
    if (isNaN(species) || isNaN(form)) continue;
    formNames.set(`${species}_${form}`, parts[2]!.replace(/\r$/, ''));
  }
}

function buildDefaultBaseStats(): void {
  const add = (id: number, hp: number, atk: number, def: number, spa: number, spd: number, spe: number) => {
    baseStats.set(`${id}_0`, { hp, atk, def, spa, spd, spe });
  };
  add(  1, 45, 49, 49, 65, 65, 45);
  add(  2, 60, 62, 63, 80, 80, 60);
  add(  3, 80, 82, 83,100,100, 80);
  add(  4, 39, 52, 43, 60, 50, 65);
  add(  5, 58, 64, 58, 80, 65, 80);
  add(  6, 78, 84, 78,109, 85,100);
  add(  7, 44, 48, 65, 50, 64, 43);
  add(  8, 59, 63, 80, 65, 80, 58);
  add(  9, 79, 83,100, 85,105, 78);
  add(152, 45, 49, 65, 49, 45, 45);
  add(153, 60, 62, 80, 63, 63, 60);
  add(154, 80, 82,100, 83, 83, 80);
  add(155, 39, 52, 43, 60, 50, 65);
  add(156, 58, 64, 58, 80, 65, 80);
  add(157, 78, 84, 78,109, 85,100);
  add(158, 50, 65, 64, 44, 48, 43);
  add(159, 65, 80, 80, 59, 63, 58);
  add(160, 85,105,100, 79, 83, 78);
  add(175, 35, 20, 35, 50, 35, 50);
  add(176, 55, 40, 85, 80, 80, 40);
  add(468, 85, 50, 95, 80,115, 80);
}

export async function initialize(dataBaseUrl: string = '/data'): Promise<void> {
  const [charmapTxt, speciesTxt, movesTxt, itemsTxt, abilitiesTxt,
         ballsTxt, locationsTxt, genderlessTxt, basestatsCsv,
         growthCsv, formsTxt] = await Promise.all([
    fetchText(`${dataBaseUrl}/charmap.txt`),
    fetchText(`${dataBaseUrl}/species.txt`),
    fetchText(`${dataBaseUrl}/moves.txt`),
    fetchText(`${dataBaseUrl}/items.txt`),
    fetchText(`${dataBaseUrl}/abilities.txt`),
    fetchText(`${dataBaseUrl}/balls.txt`),
    fetchText(`${dataBaseUrl}/locations.txt`),
    fetchText(`${dataBaseUrl}/genderless.txt`),
    fetchText(`${dataBaseUrl}/basestats.csv`),
    fetchText(`${dataBaseUrl}/growth_rates.csv`),
    fetchText(`${dataBaseUrl}/forms.txt`),
  ]);

  initializeCharmap(charmapTxt);
  speciesNames  = loadLines(speciesTxt);
  moveNames     = loadLines(movesTxt);
  itemNames     = loadLines(itemsTxt);
  abilityNames  = loadLines(abilitiesTxt);
  ballNames     = loadLines(ballsTxt);
  parseLocations(locationsTxt);
  parseGenderless(genderlessTxt);
  parseGrowthRates(growthCsv);
  parseBaseStats(basestatsCsv);
  parseFormNames(formsTxt);
  spriteDir = `${dataBaseUrl}/sprites`;
}

// ==================== 이름 조회 ====================

export function getSpeciesNames(): readonly string[] { return speciesNames; }
export function getMoveNames(): readonly string[] { return moveNames; }
export function getItemNames(): readonly string[] { return itemNames; }
export function getAbilityNames(): readonly string[] { return abilityNames; }
export function getBallNames(): readonly string[] { return ballNames; }
export function getLocationEntries(): ReadonlyMap<number, string> { return locationNames; }

export function getSpeciesName(id: number): string {
  if (id <= 0) return '없음';
  if (id < speciesNames.length && speciesNames[id]) return speciesNames[id]!;
  return `종족 #${id}`;
}

export function getMoveName(id: number): string {
  if (id <= 0) return '없음';
  if (id < moveNames.length && moveNames[id]) return moveNames[id]!;
  return `기술 #${id}`;
}

export function getItemName(id: number): string {
  if (id <= 0) return '없음';
  if (id < itemNames.length && itemNames[id]) return itemNames[id]!;
  return `아이템 #${id}`;
}

export function getAbilityName(id: number): string {
  if (id <= 0) return '없음';
  if (id < abilityNames.length && abilityNames[id]) return abilityNames[id]!;
  return `특성 #${id}`;
}

export function getBallName(id: number): string {
  if (id <= 0) return '없음';
  if (id < ballNames.length && ballNames[id]) return ballNames[id]!;
  return `볼 #${id}`;
}

export function getLocationName(id: number): string {
  const name = locationNames.get(id);
  if (name) return name;
  return `장소 #${id}`;
}

export function getLanguageName(lang: number): string {
  switch (lang) {
    case 1: return 'JPN'; case 2: return 'ENG'; case 3: return 'FRE';
    case 4: return 'ITA'; case 5: return 'GER'; case 7: return 'SPA'; case 8: return 'KOR';
    default: return `Lang${lang}`;
  }
}

// ==================== 폼 ====================

export function getFormName(species: number, form: number): string | null {
  return formNames.get(`${species}_${form}`) ?? null;
}

export function getAvailableForms(species: number): number[] {
  const forms: number[] = [];
  for (const key of baseStats.keys()) {
    const parts = key.split('_');
    if (parseInt(parts[0]!, 10) === species) {
      forms.push(parseInt(parts[1]!, 10));
    }
  }
  forms.sort((a, b) => a - b);
  if (forms.length === 0) forms.push(0);
  return forms;
}

// ==================== 기본 스탯 ====================

export function getBaseStats(species: number, form: number = 0): BaseStatsRecord | null {
  if (species <= 0) return null;
  const r = baseStats.get(`${species}_${form}`);
  if (r) return r;
  if (form !== 0) {
    const r0 = baseStats.get(`${species}_0`);
    if (r0) return r0;
  }
  return null;
}

// ==================== 성별 비율 ====================

export function getGenderRatio(species: number): number {
  if (species > 0 && species < genderRatioTable.length) return genderRatioTable[species]!;
  return 127;
}

// ==================== 성장속도 & 경험치 ====================

export function getGrowthRate(species: number): number {
  if (species > 0 && species < growthRateTable.length) return growthRateTable[species]!;
  return 0;
}

function erraticExp(n: number): number {
  if (n < 50) return Math.floor(n * n * n * (100 - n) / 50);
  if (n < 68) return Math.floor(n * n * n * (150 - n) / 100);
  if (n < 98) return Math.floor(n * n * n * Math.floor((1911 * n + 1637500) / 3) / 500000);
  return Math.floor(n * n * n * (160 - n) / 100);
}

function fluctuatingExp(n: number): number {
  if (n < 15) return Math.floor(n * n * n * (Math.floor((n + 1) / 3) + 24) / 50);
  if (n < 36) return Math.floor(n * n * n * (n + 14) / 50);
  return Math.floor(n * n * n * (Math.floor(n / 2) + 32) / 50);
}

export function getExpTable(growthRate: number, level: number): number {
  if (level <= 0) return 0;
  if (level >= 100) level = 100;
  const n = level;
  switch (growthRate) {
    case 0: return n * n * n;                                                          // MediumFast
    case 1: return erraticExp(n);                                                       // Erratic
    case 2: return fluctuatingExp(n);                                                   // Fluctuating
    case 3: return Math.max(0, Math.floor(6 * n * n * n / 5 - 15 * n * n + 100 * n - 140)); // MediumSlow
    case 4: return Math.floor(4 * n * n * n / 5);                                      // Fast
    case 5: return Math.floor(5 * n * n * n / 4);                                      // Slow
    default: return n * n * n;
  }
}

export function calcLevel(exp: number, growthRate: number): number {
  for (let lv = 100; lv >= 1; lv--) {
    if (exp >= getExpTable(growthRate, lv)) return lv;
  }
  return 1;
}

export function calcExpForLevel(level: number, species: number): number {
  return getExpTable(getGrowthRate(species), level);
}

// ==================== 스프라이트 ====================

export function getSpriteUrl(species: number, form: number = 0): string {
  if (species <= 0 || !spriteDir) return '';
  if (form > 0) return `${spriteDir}/${species}_${form}.png`;
  return `${spriteDir}/${species}.png`;
}

export function getEggSpriteUrl(): string {
  if (!spriteDir) return '';
  return `${spriteDir}/egg.png`;
}
