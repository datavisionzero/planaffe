// The performance budget of `docs/human-interface.md`, held against a build.
//
// The numbers live in that document and not here, because that is where the
// paragraph they belong to stands and a second file is one nobody looks in.
// This reads them back out of it and measures the build against them, so the
// prose and the check cannot drift apart: raising a limit means editing the
// sentence that promises it.
//
// What is measured comes out of the built `index.html`, which is the honest
// answer to "what does a browser fetch before the shell renders" — the entry
// module, every chunk preloaded beside it, and the stylesheet. The editor is
// its own chunk because it is fetched when a field is first put on a screen
// (ADR 0006), and it is found by that name rather than by a hash that changes
// with every build.
//
// An excess exits non-zero, which makes CI red and holds the trunk (ADR 0001).
// That is the point at which a budget does anything at all; the limits are set
// wide enough that the red means a jump and not a week of growth.

import { readdirSync, readFileSync, statSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const web = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const document = join(web, "../../docs/human-interface.md");
const out = resolve(web, process.argv[2] ?? "../Planaffe.Api/wwwroot");

/** kB as the build reports it, and as the document writes it. */
const kB = 1000;

/**
 * The limits, out of the table in the document: a row per budget, its name in
 * backticks in the first cell and its number in the second.
 */
function limits() {
  const found = new Map();

  for (const line of readFileSync(document, "utf8").split("\n")) {
    const row = /^\|\s*`([a-z-]+)`\s*\|\s*([\d.]+)\s*kB\s*\|/.exec(line);
    if (row !== null) found.set(row[1], Number(row[2]) * kB);
  }

  return found;
}

const bytes = (file) => statSync(join(out, file)).size;

/** Everything the built `index.html` asks for before the shell renders. */
function firstLoad() {
  const html = readFileSync(join(out, "index.html"), "utf8");
  const asked = [...html.matchAll(/(?:src|href)="\/(assets\/[^"]+\.(?:js|css))"/g)].map((match) => match[1]);

  if (asked.length === 0) throw new Error(`${out}/index.html asks for no asset — is this a build?`);

  return asked.reduce((total, file) => total + bytes(file), 0);
}

/** The editor's own chunk, named after the module it was cut from. */
function editor() {
  const chunks = readdirSync(join(out, "assets")).filter((file) => /^Editor-.*\.js$/.test(file));

  if (chunks.length !== 1) throw new Error(`expected one Editor chunk in ${out}/assets, found ${chunks.length}`);

  return bytes(join("assets", chunks[0]));
}

const measured = { "first-load": firstLoad(), editor: editor() };
const limit = limits();
let over = false;

for (const [name, weight] of Object.entries(measured)) {
  const allowed = limit.get(name);

  if (allowed === undefined) {
    console.error(`✗ ${name}: no limit in docs/human-interface.md`);
    over = true;
    continue;
  }

  const said = `${(weight / kB).toFixed(2)} kB of ${(allowed / kB).toFixed(0)} kB`;

  if (weight > allowed) {
    console.error(`✗ ${name}: ${said} — over budget by ${((weight - allowed) / kB).toFixed(2)} kB`);
    over = true;
  } else {
    console.log(`✓ ${name}: ${said}`);
  }
}

if (over) {
  console.error("\nThe budget is in docs/human-interface.md, under “Accessibility and performance floor”.");
  console.error("Either the weight comes back down, or that paragraph is rewritten to promise something else.");
  process.exit(1);
}
