import type { DocumentKeyType } from '@/service/api-types';

/**
 * Decode a document/search-result key.
 *
 * Keys travel the wire as raw bytes (base64 in JSON, per `CollectionSearchResultItem.Key`). Mirrors
 * `CollectionsController.TryResolveKey` on the server: guid = 16 bytes, long = 8, int = 4, otherwise
 * UTF-8 string. Byte layout matches .NET `BitConverter.GetBytes` (little-endian) and
 * `Guid.ToByteArray()` (mixed-endian: first 3 groups little-endian, last 2 groups as-is).
 *
 * @param base64 Base64-encoded key bytes
 * @param keyType Force an interpretation instead of auto-detecting from the byte length
 */
export function decodeKey(base64: string, keyType?: DocumentKeyType): string {
  let bytes: Uint8Array;

  try {
    bytes = base64ToBytes(base64);
  } catch {
    return base64;
  }

  const type = keyType ?? autoDetectKeyType(bytes.length);

  try {
    switch (type) {
      case 'guid':
        return bytes.length === 16 ? bytesToGuid(bytes) : base64;
      case 'long':
        return bytes.length === 8 ? new DataView(bytes.buffer, bytes.byteOffset, 8).getBigInt64(0, true).toString() : base64;
      case 'int':
        return bytes.length === 4 ? String(new DataView(bytes.buffer, bytes.byteOffset, 4).getInt32(0, true)) : base64;
      case 'string':
      default:
        return new TextDecoder('utf-8', { fatal: false }).decode(bytes);
    }
  } catch {
    return base64;
  }
}

function autoDetectKeyType(byteLength: number): DocumentKeyType {
  if (byteLength === 16) return 'guid';
  if (byteLength === 8) return 'long';
  if (byteLength === 4) return 'int';
  return 'string';
}

function base64ToBytes(base64: string): Uint8Array {
  const binary = atob(base64);
  const bytes = new Uint8Array(binary.length);

  for (let i = 0; i < binary.length; i += 1) {
    bytes[i] = binary.charCodeAt(i);
  }

  return bytes;
}

function bytesToGuid(b: Uint8Array): string {
  const hex = (values: number[]) => values.map(v => v.toString(16).padStart(2, '0')).join('');

  const p1 = hex([b[3], b[2], b[1], b[0]]);
  const p2 = hex([b[5], b[4]]);
  const p3 = hex([b[7], b[6]]);
  const p4 = hex([b[8], b[9]]);
  const p5 = hex([b[10], b[11], b[12], b[13], b[14], b[15]]);

  return `${p1}-${p2}-${p3}-${p4}-${p5}`;
}
