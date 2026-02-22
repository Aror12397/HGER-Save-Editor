import { useState, useRef, useCallback, useMemo, useEffect } from 'react';
import { createPortal } from 'react-dom';
import { PK4 } from '../core/PK4';
import * as GameData from '../core/GameData';
import { SearchComboBox } from './SearchComboBox';
import { MoveRow } from './MoveRow';
import { StatRow } from './StatRow';

interface PokemonEditorProps {
  pk: PK4;
  isPartyForm: boolean;
  onConfirm: (pk: PK4) => void;
  onCancel: () => void;
  defaultOTName?: string;
  defaultTID?: number;
  defaultSID?: number;
  defaultGender?: number;
}

const STAT_LABELS = ['공격', '방어', '스피드', '특공', '특방'] as const;
const STAT_NAMES = ['HP', '공격', '방어', '특공', '특방', '스피드'] as const;
const NAT_MAP = [-1, 0, 1, 3, 4, 2] as const;
const HIDDEN_POWER_TYPE_MAP = [6, 9, 7, 8, 12, 11, 13, 16, 1, 2, 4, 3, 10, 5, 14, 15] as const;
const LANG_IDS = [1, 2, 3, 4, 5, 7, 8] as const;
const LANG_NAMES = ['JPN (1)', 'ENG (2)', 'FRE (3)', 'ITA (4)', 'GER (5)', 'SPA (7)', 'KOR (8)'] as const;

export function PokemonEditor({ pk, isPartyForm, onConfirm, onCancel, defaultOTName, defaultTID = 0, defaultSID = 0, defaultGender = 0 }: PokemonEditorProps) {
  const isNewSlot = pk.isEmpty;
  const workingPk = useRef(isNewSlot
    ? PK4.createBlank(isPartyForm ? 236 : 136)
    : pk.clone()
  ).current;

  const [activeTab, setActiveTab] = useState(0);

  // ==================== 기본 탭 상태 ====================
  const [species, setSpecies] = useState(workingPk.species);
  const [form, setForm] = useState(workingPk.form);
  const [nickname, setNickname] = useState(workingPk.isNicknamed ? workingPk.nickname : GameData.getSpeciesName(workingPk.species));
  const [isNicknamed, setIsNicknamed] = useState(workingPk.isNicknamed);
  const [level, setLevel] = useState(Math.max(1, Math.min(100, workingPk.level)));
  const [exp, setExp] = useState(workingPk.exp);
  const [mintNature, setMintNature] = useState(() => {
    const raw = workingPk.statNature;
    return (raw >= 2 && raw % 2 === 0) ? raw / 2 : 0;
  });
  const [abilityId, setAbilityId] = useState(workingPk.ability);
  const [heldItemId, setHeldItemId] = useState(workingPk.heldItem);
  const [friendship, setFriendship] = useState(workingPk.friendship);
  const [langIndex, setLangIndex] = useState(() => {
    const idx = LANG_IDS.indexOf(workingPk.language as typeof LANG_IDS[number]);
    return idx >= 0 ? idx : 1;
  });
  const [ballId, setBallId] = useState(workingPk.ball);

  // ==================== 기술 탭 상태 ====================
  const [moves, setMoves] = useState([
    { id: workingPk.move1 === 0xFFFF ? 0 : workingPk.move1, pp: workingPk.pp1, ppUp: workingPk.ppUp1 },
    { id: workingPk.move2 === 0xFFFF ? 0 : workingPk.move2, pp: workingPk.pp2, ppUp: workingPk.ppUp2 },
    { id: workingPk.move3 === 0xFFFF ? 0 : workingPk.move3, pp: workingPk.pp3, ppUp: workingPk.ppUp3 },
    { id: workingPk.move4 === 0xFFFF ? 0 : workingPk.move4, pp: workingPk.pp4, ppUp: workingPk.ppUp4 },
  ]);

  // ==================== 스탯 탭 상태 ====================
  const [ivs, setIvs] = useState([workingPk.ivHP, workingPk.ivAtk, workingPk.ivDef, workingPk.ivSpA, workingPk.ivSpD, workingPk.ivSpe]);
  const [evs, setEvs] = useState([workingPk.evHP, workingPk.evAtk, workingPk.evDef, workingPk.evSpA, workingPk.evSpD, workingPk.evSpe]);

  // ==================== 만남 탭 상태 ====================
  const [otName, setOtName] = useState(isNewSlot && defaultOTName ? defaultOTName : workingPk.otName);
  const [tid, setTid] = useState(isNewSlot ? defaultTID : workingPk.tid);
  const [sid, setSid] = useState(isNewSlot ? defaultSID : workingPk.sid);
  const [metLevel, setMetLevel] = useState(workingPk.metLevel);
  const [metLocationId, setMetLocationId] = useState(workingPk.metLocation);
  const [metDate, setMetDate] = useState(() => {
    if (isNewSlot) {
      const now = new Date();
      return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`;
    }
    const d = workingPk.metDate;
    return d.year > 0 ? `20${String(d.year).padStart(2, '0')}-${String(d.month).padStart(2, '0')}-${String(d.day).padStart(2, '0')}` : '';
  });

  // PID (read-only)
  const pidHex = workingPk.pid.toString(16).toUpperCase().padStart(8, '0');
  const pidNature = workingPk.pid % 25;

  // ==================== 파생 계산 ====================

  const effectiveNature = mintNature > 0 ? mintNature - 1 : pidNature;

  const isShiny = useMemo(() => {
    const p = workingPk.pid;
    return (tid ^ sid ^ ((p >>> 16) & 0xFFFF) ^ (p & 0xFFFF)) < 8;
  }, [tid, sid, workingPk.pid]);

  const genderDisplay = useMemo(() => {
    const threshold = GameData.getGenderRatio(species);
    if (threshold === 255) return { text: '무성', color: '#C0C0C0' };
    if (threshold === 254) return { text: '♀ 암컷', color: '#FFB6C1' };
    if (threshold === 0) return { text: '♂ 수컷', color: '#E0FFFF' };
    return (workingPk.pid & 0xFF) < threshold
      ? { text: '♀ 암컷', color: '#FFB6C1' }
      : { text: '♂ 수컷', color: '#E0FFFF' };
  }, [species, workingPk.pid]);

  const availableForms = useMemo(() => GameData.getAvailableForms(species), [species]);
  const hasMultipleForms = availableForms.length > 1;

  const baseStats = useMemo(() => GameData.getBaseStats(species, form), [species, form]);

  const calcStats = useMemo(() => {
    if (!baseStats || level <= 0) return null;
    const bs = [baseStats.hp, baseStats.atk, baseStats.def, baseStats.spa, baseStats.spd, baseStats.spe];
    return bs.map((b, i) => {
      const iv = ivs[i]!; const ev = evs[i]!;
      const natIdx = NAT_MAP[i]!;
      const mod = (i === 0 || natIdx < 0) ? 1.0 : GameData.getNatureModifier(effectiveNature, natIdx);
      if (i === 0) return Math.floor((2 * b + iv + Math.floor(ev / 4)) * level / 100) + level + 10;
      return Math.floor((Math.floor((2 * b + iv + Math.floor(ev / 4)) * level / 100) + 5) * mod);
    });
  }, [baseStats, level, ivs, evs, effectiveNature]);

  const evTotal = useMemo(() => evs.reduce((s, v) => s + v, 0), [evs]);

  const hiddenPowerType = useMemo(() => {
    const bits = [ivs[0]! & 1, ivs[1]! & 1, ivs[2]! & 1, ivs[3]! & 1, ivs[4]! & 1, ivs[5]! & 1];
    const idx = Math.floor((bits[0]! + 2 * bits[1]! + 4 * bits[2]! + 8 * bits[5]! + 16 * bits[3]! + 32 * bits[4]!) * 15 / 63);
    const typeIdx = HIDDEN_POWER_TYPE_MAP[Math.max(0, Math.min(15, idx))]!;
    return typeIdx < GameData.TypeNames.length ? GameData.TypeNames[typeIdx]! : '???';
  }, [ivs]);

  const getNatureModLabel = useCallback((nat: number) => {
    if (nat < 0 || nat > 24) return '';
    const b = Math.floor(nat / 5); const r = nat % 5;
    return b === r ? '(무보정)' : `(${STAT_LABELS[b]}↑  ${STAT_LABELS[r]}↓)`;
  }, []);

  const getStatNameColor = useCallback((statIdx: number) => {
    const natIdx = NAT_MAP[statIdx]!;
    if (natIdx < 0) return undefined;
    const b = Math.floor(effectiveNature / 5); const r = effectiveNature % 5;
    if (b === r) return undefined;
    if (natIdx === b) return '#FF8C8C';
    if (natIdx === r) return '#8CA0FF';
    return undefined;
  }, [effectiveNature]);

  // ==================== 스프라이트 ====================
  const [spriteFailed, setSpriteFailed] = useState(false);
  useEffect(() => setSpriteFailed(false), [species, form]);
  const spriteUrl = spriteFailed ? GameData.getSpriteUrl(species, 0) : GameData.getSpriteUrl(species, form);

  // ==================== Level/EXP sync ====================
  const syncRef = useRef(false);
  const handleLevelChange = (v: number) => {
    if (syncRef.current) return;
    syncRef.current = true;
    const lv = Math.max(1, Math.min(100, v));
    setLevel(lv);
    setExp(GameData.calcExpForLevel(lv, species));
    syncRef.current = false;
  };
  const handleExpChange = (v: number) => {
    if (syncRef.current) return;
    syncRef.current = true;
    setExp(v);
    setLevel(GameData.calcLevel(v, GameData.getGrowthRate(species)));
    syncRef.current = false;
  };

  // ==================== EV enforcement ====================
  const handleEVChange = (idx: number, value: number) => {
    setEvs(prev => {
      const next = [...prev];
      next[idx] = value;
      const total = next.reduce((s, v) => s + v, 0);
      if (total > 510) next[idx] = Math.max(0, value - (total - 510));
      return next;
    });
  };

  // ==================== Move helpers ====================
  const updateMove = (idx: number, field: 'id' | 'pp' | 'ppUp', value: number) => {
    setMoves(prev => prev.map((m, i) => i === idx ? { ...m, [field]: value } : m));
  };

  // ==================== Location items ====================
  const locationItems = useMemo(() => {
    const entries = GameData.getLocationEntries();
    let maxId = 0;
    for (const k of entries.keys()) if (k > maxId) maxId = k;
    maxId = Math.max(maxId, 300);
    const result: string[] = [];
    for (let i = 0; i <= maxId; i++) result.push(GameData.getLocationName(i));
    return result;
  }, []);

  // ==================== 확인/취소 ====================
  const handleConfirm = () => {
    const p = workingPk;
    p.species = species;
    p.form = form;
    p.nickname = nickname;
    p.isNicknamed = isNicknamed;
    p.exp = exp;
    p.ability = abilityId;
    p.heldItem = heldItemId;
    p.friendship = friendship;
    p.language = LANG_IDS[langIndex]!;
    p.ball = ballId;
    p.statNature = mintNature * 2;

    p.move1 = moves[0]!.id; p.pp1 = moves[0]!.pp; p.ppUp1 = moves[0]!.ppUp;
    p.move2 = moves[1]!.id; p.pp2 = moves[1]!.pp; p.ppUp2 = moves[1]!.ppUp;
    p.move3 = moves[2]!.id; p.pp3 = moves[2]!.pp; p.ppUp3 = moves[2]!.ppUp;
    p.move4 = moves[3]!.id; p.pp4 = moves[3]!.pp; p.ppUp4 = moves[3]!.ppUp;

    p.ivHP = ivs[0]!; p.evHP = evs[0]!;
    p.ivAtk = ivs[1]!; p.evAtk = evs[1]!;
    p.ivDef = ivs[2]!; p.evDef = evs[2]!;
    p.ivSpA = ivs[3]!; p.evSpA = evs[3]!;
    p.ivSpD = ivs[4]!; p.evSpD = evs[4]!;
    p.ivSpe = ivs[5]!; p.evSpe = evs[5]!;

    p.otName = otName;
    p.tid = tid;
    p.sid = sid;
    p.metLevel = metLevel;
    p.metLocation = metLocationId;

    const match = metDate.match(/^(\d{4})-(\d{2})-(\d{2})$/);
    if (match) {
      p.metDate = { year: parseInt(match[1]!, 10) - 2000, month: parseInt(match[2]!, 10), day: parseInt(match[3]!, 10) };
    }

    if (isNewSlot) {
      p.otIsFemale = defaultGender === 1;
      p.originGame = 7;
    }

    p.refreshStats();

    const warnings = p.getValidationWarnings();
    if (warnings.length > 0) {
      const msg = `이 포켓몬에 다음 문제가 있습니다:\n\n${warnings.map(w => '• ' + w).join('\n')}\n\n이대로 저장하시겠습니까?`;
      if (!confirm(msg)) return;
    }

    onConfirm(p);
  };

  // ==================== 렌더링 ====================
  const tabs = ['기본', '기술', '스탯 / EV·IV', '만남 / OT'];

  return createPortal(
    <div style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.6)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 100 }}
      onMouseDown={(e) => { if (e.target === e.currentTarget) onCancel(); }}>
      <div className="editor-modal" style={{ background: '#1E1E1E', width: 580, maxHeight: '90vh', border: '1px solid #505050', display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
        {/* 탭 헤더 */}
        <div className="editor-tabs" style={{ display: 'flex', background: '#2D2D30', borderBottom: '1px solid #505050' }}>
          {tabs.map((t, i) => (
            <button key={t} onClick={() => setActiveTab(i)} style={{
              padding: '8px 16px', background: i === activeTab ? '#1E1E1E' : 'transparent',
              color: i === activeTab ? '#FFF' : '#AAA', border: 'none', borderBottom: i === activeTab ? '2px solid #007ACC' : '2px solid transparent',
              cursor: 'pointer', fontSize: '13px', flex: '1 1 0',
            }}>{t}</button>
          ))}
          {/* 스프라이트 */}
          {spriteUrl && (
            <div style={{ marginLeft: 'auto', padding: 4 }}>
              <img src={spriteUrl} onError={() => setSpriteFailed(true)} width={48} height={48} style={{ background: '#323237' }} />
            </div>
          )}
        </div>

        {/* 탭 내용 */}
        <div style={{ flex: 1, overflow: 'auto', padding: 16 }}>
          {activeTab === 0 && (
            <div className="editor-grid" style={{ display: 'grid', gridTemplateColumns: '100px 1fr', gap: '8px 12px', alignItems: 'center' }}>
              <span style={labelStyle}>PID (hex)</span>
              <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                <span style={{ width: 22, height: 22, display: 'flex', alignItems: 'center', justifyContent: 'center', background: isShiny ? '#786414' : '#3C3C3C', color: isShiny ? '#FFD700' : '#808080', fontSize: '12px', borderRadius: 2 }}>
                  {isShiny ? '★' : '☆'}
                </span>
                <input value={pidHex} disabled style={{ width: 100, background: '#3C3C3C', color: '#808080' }} />
              </div>

              <span style={labelStyle}>종류</span>
              <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                <SearchComboBox items={GameData.getSpeciesNames().slice(0, 1026)} selectedId={species} onSelect={(id) => {
                  setSpecies(id);
                  if (!isNicknamed) setNickname(GameData.getSpeciesName(id));
                  const forms = GameData.getAvailableForms(id);
                  if (!forms.includes(form)) setForm(forms[0] ?? 0);
                }} width={200} />
                {hasMultipleForms && <>
                  <span style={{ color: '#808080', fontSize: '12px' }}>폼</span>
                  <select value={form} onChange={(e) => setForm(Number(e.target.value))} style={{ width: 120 }}>
                    {availableForms.map(f => (
                      <option key={f} value={f}>{GameData.getFormName(species, f) ?? `폼 ${f}`}</option>
                    ))}
                  </select>
                </>}
              </div>

              <span style={labelStyle}>닉네임</span>
              <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                <input value={nickname} onChange={(e) => setNickname(e.target.value)} maxLength={10} style={{ width: 130 }} readOnly={!isNicknamed} />
                <label style={{ color: '#C0C0C0', fontSize: '13px', display: 'flex', alignItems: 'center', gap: 4 }}>
                  <input type="checkbox" checked={isNicknamed} onChange={(e) => {
                    setIsNicknamed(e.target.checked);
                    if (!e.target.checked) setNickname(GameData.getSpeciesName(species));
                  }} />
                  닉네임 지정
                </label>
              </div>

              <span style={labelStyle}>레벨 / EXP</span>
              <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                <input value={level} onChange={(e) => { const v = parseInt(e.target.value, 10); if (!isNaN(v)) handleLevelChange(v); }} style={{ width: 55, textAlign: 'right' }} maxLength={3} />
                <span style={{ color: '#808080', fontSize: '12px' }}>EXP</span>
                <input value={exp} onChange={(e) => { const v = parseInt(e.target.value, 10); if (!isNaN(v)) handleExpChange(v); }} style={{ width: 110, textAlign: 'right' }} maxLength={9} />
              </div>

              <span style={labelStyle}>성격</span>
              <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                <select value={pidNature} disabled style={{ width: 110, background: '#3C3C3C', color: '#808080' }}>
                  {GameData.NatureNames.map((n, i) => <option key={i} value={i}>{n}</option>)}
                </select>
                <span style={{ color: '#DDA0DD', fontSize: '12px' }}>{getNatureModLabel(pidNature)}</span>
              </div>

              <span style={labelStyle}>민트</span>
              <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                <select value={mintNature} onChange={(e) => setMintNature(Number(e.target.value))} style={{ width: 110 }}>
                  <option value={0}>없음 (PID 성격)</option>
                  {GameData.NatureNames.map((n, i) => <option key={i} value={i + 1}>{n}</option>)}
                </select>
                {mintNature > 0 && <span style={{ color: '#82DC82', fontSize: '12px' }}>{getNatureModLabel(mintNature - 1)}</span>}
              </div>

              <span style={labelStyle}>성별</span>
              <span style={{ color: genderDisplay.color, fontWeight: 'bold' }}>{genderDisplay.text}</span>

              <span style={labelStyle}>특성</span>
              <SearchComboBox items={GameData.getAbilityNames()} selectedId={abilityId} onSelect={setAbilityId} width={200} />

              <span style={labelStyle}>지닌 아이템</span>
              <SearchComboBox items={GameData.getItemNames()} selectedId={heldItemId} onSelect={setHeldItemId} width={200} />

              <span style={labelStyle}>친밀도</span>
              <input value={friendship} onChange={(e) => { const v = parseInt(e.target.value, 10); if (!isNaN(v)) setFriendship(Math.min(255, Math.max(0, v))); }} style={{ width: 70, textAlign: 'right' }} maxLength={3} />

              <span style={labelStyle}>언어</span>
              <select value={langIndex} onChange={(e) => setLangIndex(Number(e.target.value))} style={{ width: 110 }}>
                {LANG_NAMES.map((n, i) => <option key={i} value={i}>{n}</option>)}
              </select>

              <span style={labelStyle}>볼</span>
              <select value={ballId} onChange={(e) => setBallId(Number(e.target.value))} style={{ width: 140 }}>
                {GameData.getBallNames().map((n, i) => <option key={i} value={i}>{n || `볼 ${i}`}</option>)}
              </select>
            </div>
          )}

          {activeTab === 1 && (
            <div>
              <div style={{ display: 'flex', gap: 8, marginBottom: 8, color: '#C0C0C0', fontSize: '13px' }}>
                <span style={{ width: 50 }}>슬롯</span>
                <span style={{ width: 200 }}>기술</span>
                <span style={{ width: 40, textAlign: 'right' }}>PP</span>
                <span style={{ width: 30, textAlign: 'right' }}>PP업</span>
              </div>
              {moves.map((m, i) => (
                <MoveRow key={i} slotNum={i + 1} moveId={m.id} pp={m.pp} ppUp={m.ppUp}
                  onMoveChange={(id) => updateMove(i, 'id', id)}
                  onPPChange={(pp) => updateMove(i, 'pp', pp)}
                  onPPUpChange={(ppUp) => updateMove(i, 'ppUp', ppUp)}
                />
              ))}
            </div>
          )}

          {activeTab === 2 && (
            <div>
              <div style={{ display: 'flex', gap: 8, marginBottom: 8, color: '#C0C0C0', fontSize: '13px' }}>
                <span style={{ width: 50 }}>스탯</span>
                <span style={{ width: 35, textAlign: 'right' }}>기본</span>
                <span style={{ width: 40, textAlign: 'right' }}>IV</span>
                <span style={{ width: 45, textAlign: 'right' }}>EV</span>
                <span style={{ width: 40, textAlign: 'right' }}>계산</span>
              </div>
              {STAT_NAMES.map((name, i) => (
                <StatRow key={i} name={name}
                  baseStat={baseStats ? [baseStats.hp, baseStats.atk, baseStats.def, baseStats.spa, baseStats.spd, baseStats.spe][i]! : null}
                  iv={ivs[i]!} ev={evs[i]!}
                  calcValue={calcStats ? calcStats[i]! : null}
                  nameColor={getStatNameColor(i)}
                  onIVChange={(v) => setIvs(prev => prev.map((old, j) => j === i ? v : old))}
                  onEVChange={(v) => handleEVChange(i, v)}
                />
              ))}
              <div style={{ display: 'flex', gap: 12, alignItems: 'center', marginTop: 12, flexWrap: 'wrap' }}>
                <span style={{ color: '#C0C0C0', fontSize: '13px' }}>잠재파워: {hiddenPowerType}</span>
                <span style={{ color: evTotal > 510 ? '#FF6347' : evTotal === 510 ? '#90EE90' : '#C0C0C0', fontSize: '13px' }}>
                  EV: {evTotal} / 510
                </span>
                <button onClick={() => setIvs([31, 31, 31, 31, 31, 31])} style={smallBtnStyle}>IV 최대</button>
                <button onClick={() => setIvs([0, 0, 0, 0, 0, 0])} style={smallBtnStyle}>IV 초기화</button>
                <button onClick={() => setEvs([0, 0, 0, 0, 0, 0])} style={smallBtnStyle}>EV 초기화</button>
              </div>
            </div>
          )}

          {activeTab === 3 && (
            <div className="editor-grid" style={{ display: 'grid', gridTemplateColumns: '100px 1fr', gap: '8px 12px', alignItems: 'center' }}>
              <span style={labelStyle}>어버이 이름</span>
              <input value={otName} onChange={(e) => setOtName(e.target.value)} maxLength={7} style={{ width: 110 }} />

              <span style={labelStyle}>TID / SID</span>
              <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                <input value={tid} onChange={(e) => { const v = parseInt(e.target.value, 10); if (!isNaN(v)) setTid(Math.min(65535, Math.max(0, v))); }} style={{ width: 75, textAlign: 'right' }} maxLength={5} />
                <span style={{ color: '#808080', fontSize: '12px' }}>SID</span>
                <input value={sid} onChange={(e) => { const v = parseInt(e.target.value, 10); if (!isNaN(v)) setSid(Math.min(65535, Math.max(0, v))); }} style={{ width: 75, textAlign: 'right' }} maxLength={5} />
              </div>

              <span style={labelStyle}>만난 레벨</span>
              <input value={metLevel} onChange={(e) => { const v = parseInt(e.target.value, 10); if (!isNaN(v)) setMetLevel(Math.min(127, Math.max(0, v))); }} style={{ width: 65, textAlign: 'right' }} maxLength={3} />

              <span style={labelStyle}>만난 장소</span>
              <SearchComboBox items={locationItems} selectedId={metLocationId} onSelect={setMetLocationId} width={240} />

              <span style={labelStyle}>만난 날짜</span>
              <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                <input type="date" value={metDate} onChange={(e) => setMetDate(e.target.value)} style={{ width: 150 }} />
              </div>
            </div>
          )}
        </div>

        {/* 하단 버튼 */}
        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8, padding: '8px 16px', background: '#2D2D30', borderTop: '1px solid #505050' }}>
          <button onClick={onCancel} style={{ ...btnStyle, background: '#3C3C3C' }}>취소</button>
          <button onClick={handleConfirm} style={{ ...btnStyle, background: '#007ACC' }}>확인</button>
        </div>
      </div>
    </div>,
    document.body
  );
}

const labelStyle: React.CSSProperties = { color: '#C0C0C0', fontSize: '13px', textAlign: 'right' };
const btnStyle: React.CSSProperties = { padding: '6px 20px', color: '#FFF', border: 'none', cursor: 'pointer', fontSize: '13px' };
const smallBtnStyle: React.CSSProperties = { padding: '3px 10px', background: '#3C3C3C', color: '#FFF', border: '1px solid #505050', cursor: 'pointer', fontSize: '12px' };
