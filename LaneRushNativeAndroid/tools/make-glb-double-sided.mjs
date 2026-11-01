import fs from "node:fs";

const [input, output] = process.argv.slice(2);
if (!input || !output) throw new Error("usage: node make-glb-double-sided.mjs input.glb output.glb");

const source = fs.readFileSync(input);
if (source.readUInt32LE(0) !== 0x46546c67 || source.readUInt32LE(4) !== 2) {
  throw new Error("Expected a binary glTF 2.0 file");
}

const chunks = [];
for (let offset = 12; offset < source.length;) {
  const length = source.readUInt32LE(offset);
  const type = source.readUInt32LE(offset + 4);
  chunks.push({ type, data: source.subarray(offset + 8, offset + 8 + length) });
  offset += 8 + length;
}

const jsonChunk = chunks.find(({ type }) => type === 0x4e4f534a);
const document = JSON.parse(jsonChunk.data.toString("utf8").trim());
for (const material of document.materials ?? []) material.doubleSided = true;

const encoded = Buffer.from(JSON.stringify(document));
const paddedLength = Math.ceil(encoded.length / 4) * 4;
jsonChunk.data = Buffer.concat([encoded, Buffer.alloc(paddedLength - encoded.length, 0x20)]);

const totalLength = 12 + chunks.reduce((total, chunk) => total + 8 + chunk.data.length, 0);
const header = Buffer.alloc(12);
header.writeUInt32LE(0x46546c67, 0);
header.writeUInt32LE(2, 4);
header.writeUInt32LE(totalLength, 8);
const outputChunks = chunks.flatMap((chunk) => {
  const chunkHeader = Buffer.alloc(8);
  chunkHeader.writeUInt32LE(chunk.data.length, 0);
  chunkHeader.writeUInt32LE(chunk.type, 4);
  return [chunkHeader, chunk.data];
});
fs.writeFileSync(output, Buffer.concat([header, ...outputChunks]));
