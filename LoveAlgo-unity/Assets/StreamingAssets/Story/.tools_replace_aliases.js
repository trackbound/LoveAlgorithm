// Prologue.csv 한글 별칭 치환 — 1회용 (Node.js)
// 실행: node .tools_replace_aliases.js
const fs = require("fs");
const path = require("path");

const ROOT = __dirname;
const SRC = path.join(ROOT, "Prologue.csv");
const BAK = path.join(ROOT, "Prologue.csv.bak_pre_alias");

const CHAR_MAP = {
  c01: "로아", Roa: "로아",
  Daeun: "서다은", SeoDaEun: "서다은",
  Yeun: "하예은", HaYeEun: "하예은",
  Heewon: "도희원", DoHeewon: "도희원",
  Bom: "이봄", LeeBom: "이봄",
};

const EMOTE_MAP = {
  _00: "기본", _11: "눈웃음", _12: "밝게웃음", _13: "활짝웃음", _14: "행복",
  _21: "찌릿", _22: "쌔짐", _23: "머쓱", _24: "어질어질",
  _31: "울먹", _32: "주르륵", _33: "와아앙", _34: "부끄", _35: "졸려",
  _41: "깜짝", _42: "반짝빈짝", _43: "궁금", _44: "윙크", _45: "자신만만",
  _55: "음주", _56: "만취", _57: "집중", _58: "고민",
};

// 인라인 텍스트 태그용 — DialogueUI가 인식하는 영문 별칭도 한글로 매핑
const INLINE_EMOTE_MAP = {
  ...EMOTE_MAP,
  Default: "기본", EyeSmile: "눈웃음", BrightSmile: "밝게웃음",
  Happy: "행복", Glare: "찌릿", Tearful: "울먹", Surprise: "깜짝",
};

const BGM_MAP = {
  Roa: "로아", Daily2: "일상2",
  Daeun: "서다은", Yeun: "하예은", Heewon: "도희원", Bom: "이봄",
  white_noise: "백색소음",
  // Daily1, Night 등 catalog 미등록 — 폴백
};

const SD_MAP = {
  sd_c02_01: "다은 속닥",
  sd_c04_01: "희원 맥주",
  sd_c05_01: "봄 글썹",
};

const CG_MAP = {
  cg_c01_01: "로아 첫만남",
  cg_c03_01: "예은 입부신청서 작성",
};

const BG_MAP = {
  bg_00_00: "빈 화면",
  bg_10_01: "자취방 전경 낮",
  bg_10_02: "자취방 전경 밤 불꺼짐",
  bg_10_03: "자취방 전경 밤 불커짐",
  bg_10_04: "자취방 침대위 아침",
  bg_10_05: "자취방 침대위 밤",
  bg_10_06: "자취방 책상 모니터",
  bg_20_01: "공대 앞 낮",
  bg_20_02: "공대 앞 밤",
  bg_20_03: "공대 강의실복도",
  bg_20_04: "공대 학생복지실",
  bg_20_05: "공대 강의실 낮",
  bg_20_06: "공대 강의실 낮 벚꽃",
  bg_30_01: "캠퍼스거리1 낮 맑음",
  bg_30_02: "캠퍼스거리2 낮 맑음",
  bg_40_01: "학생회관 앞 낮",
  bg_40_02: "학생회관 앞 밤",
  bg_40_03: "행정실",
  bg_40_04: "학생회관 복도",
  bg_40_05: "학생회관 게시판앞",
  bg_40_06: "학생회관 동아리방 낮",
  bg_40_07: "학생회관 동아리방 벚꽃",
  bg_60_02: "편의점 앞 밤",
};

const stats = { BG: 0, CG: 0, SD: 0, Char: 0, Emote: 0, BGM: 0, InlineEmote: 0 };

// 인라인 <emote=...> 태그 치환 — Text Value 컬럼 대상
function replaceInlineEmotes(text) {
  if (!text || !text.includes("<emote=")) return text;
  return text.replace(/<emote=([^/>]+)\/?>/g, (m, code) => {
    const trimmed = code.trim();
    if (INLINE_EMOTE_MAP[trimmed]) {
      stats.InlineEmote++;
      return `<emote=${INLINE_EMOTE_MAP[trimmed]}/>`;
    }
    return m;
  });
}

// 간단 CSV 파서 — quoted field + escape ("" → ") 지원, newline은 quoted 내에서 허용
function parseCsv(text) {
  const rows = [];
  let row = [];
  let cur = "";
  let i = 0;
  let inQuotes = false;
  while (i < text.length) {
    const c = text[i];
    if (inQuotes) {
      if (c === '"' && text[i + 1] === '"') { cur += '"'; i += 2; continue; }
      if (c === '"') { inQuotes = false; i++; continue; }
      cur += c; i++; continue;
    }
    if (c === '"') { inQuotes = true; i++; continue; }
    if (c === ",") { row.push(cur); cur = ""; i++; continue; }
    if (c === "\r") { i++; continue; }
    if (c === "\n") { row.push(cur); rows.push(row); row = []; cur = ""; i++; continue; }
    cur += c; i++;
  }
  if (cur.length > 0 || row.length > 0) { row.push(cur); rows.push(row); }
  return rows;
}

function csvEscape(s) {
  if (s == null) return "";
  if (/[",\r\n]/.test(s)) return '"' + s.replace(/"/g, '""') + '"';
  return s;
}

function writeCsv(rows) {
  return rows.map(r => r.map(csvEscape).join(",")).join("\n") + "\n";
}

function replaceInValue(lineType, value) {
  if (!value) return value;
  const parts = value.split(":");

  if (lineType === "BG") {
    if (BG_MAP[parts[0]]) { parts[0] = BG_MAP[parts[0]]; stats.BG++; }
    return parts.join(":");
  }
  if (lineType === "CG") {
    if (CG_MAP[parts[0]]) { parts[0] = CG_MAP[parts[0]]; stats.CG++; }
    return parts.join(":");
  }
  if (lineType === "SD") {
    if (SD_MAP[parts[0]]) { parts[0] = SD_MAP[parts[0]]; stats.SD++; }
    return parts.join(":");
  }
  if (lineType === "Char") {
    if (parts.length >= 2) {
      const action = parts[1].toLowerCase();
      if (action === "enter" || action === "enterup") {
        if (parts.length >= 3 && CHAR_MAP[parts[2]]) { parts[2] = CHAR_MAP[parts[2]]; stats.Char++; }
        if (parts.length >= 4 && EMOTE_MAP[parts[3]]) { parts[3] = EMOTE_MAP[parts[3]]; stats.Emote++; }
      } else if (action === "emote") {
        if (parts.length >= 3 && EMOTE_MAP[parts[2]]) { parts[2] = EMOTE_MAP[parts[2]]; stats.Emote++; }
      }
    }
    return parts.join(":");
  }
  if (lineType === "Sound") {
    if (parts.length >= 2 && parts[0] === "BGM" && BGM_MAP[parts[1]]) {
      parts[1] = BGM_MAP[parts[1]]; stats.BGM++;
    }
    return parts.join(":");
  }
  return value;
}

function main() {
  if (!fs.existsSync(SRC)) {
    console.error(`ERROR: ${SRC} 없음`);
    process.exit(1);
  }
  if (!fs.existsSync(BAK)) {
    fs.copyFileSync(SRC, BAK);
    console.log(`백업 생성: ${BAK}`);
  } else {
    console.log(`백업 이미 존재: ${BAK}`);
  }

  const text = fs.readFileSync(SRC, "utf8");
  const rows = parseCsv(text);

  for (let i = 0; i < rows.length; i++) {
    const row = rows[i];
    if (row.length < 5) continue;
    if (i === 0 && row[0] === "LineID") continue; // 헤더
    const lineType = (row[1] || "").trim();
    const value = row[3] || "";
    if (lineType && value) row[3] = replaceInValue(lineType, value);

    // 인라인 <emote=...> 태그 — Text 라인의 Value 컬럼에 포함
    if (lineType === "Text" && row[3]) {
      row[3] = replaceInlineEmotes(row[3]);
    }
  }

  fs.writeFileSync(SRC, writeCsv(rows), "utf8");
  console.log("치환 완료:");
  for (const [k, v] of Object.entries(stats)) console.log(`  ${k}: ${v}건`);
}

main();
