// Prologue.csv 씬 Mark + Setup 일괄 삽입 — 1회용
// 각 씬 시작 LineID 직전에 두 줄 삽입:
//   1. ,Flow,,Mark:scene:{name},>
//   2. ,FX,,Setup:{spec},>
//
// 실행: node .tools_add_scene_marks.js
const fs = require("fs");
const path = require("path");

const ROOT = __dirname;
const SRC = path.join(ROOT, "Prologue.csv");
const BAK = path.join(ROOT, "Prologue.csv.bak_pre_scene_marks");

// [기존 LineID 직전에 삽입, 씬 이름, Setup spec (한글 alias)]
const SCENES = [
  ["pro_001", "로아 첫만남 (CG 인트로)",      "BG=빈 화면|BGM=로아"],
  ["pro_010", "자취방 모니터 — 로아 첫 대화", "BG=자취방 책상 모니터|BGM=로아|Char=로아:C"],
  ["pro_117", "다음날 기상 (침대)",           "BG=자취방 침대위 아침|Eye=Close"],
  ["pro_148", "공대 강의실 — 다은",           "BG=공대 강의실 낮|BGM=서다은"],
  ["pro_204", "캠퍼스 — 예은 충돌",           "BG=캠퍼스거리1 낮 맑음|BGM=하예은"],
  ["pro_311", "자취방 밤 — 로아와 일과정리", "BG=자취방 전경 밤 불커짐|BGM=로아"],
  ["pro_328", "다음날 — 학교",                "BG=공대 학생복지실|Eye=Open"],
  ["pro_356", "복지실 — 희원 첫만남",         "BG=공대 학생복지실|BGM=도희원"],
  ["pro_405", "학생회관 앞",                  "BG=학생회관 앞 낮"],
  ["pro_420", "행정실",                       "BG=행정실"],
  ["pro_440", "학생회관 앞 — 예은 입부",      "BG=학생회관 앞 낮|BGM=하예은"],
  ["pro_566", "자취방 모니터 — 로아",         "BG=자취방 책상 모니터|BGM=로아|Char=로아:C"],
  ["pro_586", "편의점 — 희원 만남",           "BG=bg_60_01|BGM=도희원"],
  ["pro_651", "게시판 — 봄",                  "BG=학생회관 게시판앞|BGM=이봄"],
  ["pro_701", "학생회관 복도",                "BG=학생회관 복도"],
  ["pro_752", "동아리방",                     "BG=학생회관 동아리방 낮"],
];

function main() {
  if (!fs.existsSync(SRC)) { console.error(`ERROR: ${SRC} 없음`); process.exit(1); }

  if (!fs.existsSync(BAK)) {
    fs.copyFileSync(SRC, BAK);
    console.log(`백업 생성: ${BAK}`);
  } else {
    console.log(`백업 이미 존재: ${BAK}`);
  }

  const text = fs.readFileSync(SRC, "utf8");
  const lines = text.split(/\r?\n/);

  const targetMap = new Map();
  for (const [lineId, name, spec] of SCENES) {
    targetMap.set(lineId, { name, spec });
  }

  const out = [];
  const insertedAt = [];
  const missing = new Set(targetMap.keys());

  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];
    const firstComma = line.indexOf(",");
    const lineId = firstComma > 0 ? line.substring(0, firstComma).trim() : "";

    if (lineId && targetMap.has(lineId)) {
      const { name, spec } = targetMap.get(lineId);
      out.push(`,Flow,,Mark:scene:${name},>`);
      out.push(`,FX,,Setup:${spec},>`);
      insertedAt.push({ before: lineId, name });
      missing.delete(lineId);
    }

    out.push(line);
  }

  fs.writeFileSync(SRC, out.join("\n"), "utf8");
  console.log(`\n삽입 완료: ${insertedAt.length}개 씬 마크`);
  insertedAt.forEach((s, i) => {
    console.log(`  ${String(i + 1).padStart(2)}. before ${s.before.padEnd(10)} : ${s.name}`);
  });

  if (missing.size > 0) {
    console.log(`\n⚠ 못 찾은 LineID (CSV에 없음):`);
    for (const id of missing) console.log(`  - ${id}`);
  }
}

main();
