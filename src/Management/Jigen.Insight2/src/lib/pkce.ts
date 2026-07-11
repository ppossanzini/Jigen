const toBase64Url = (bytes: Uint8Array): string => {
  let binary = ''

  bytes.forEach((byte) => {
    binary += String.fromCharCode(byte)
  })

  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/g, '')
}

export const createRandomToken = (size: number): string => {
  const bytes = new Uint8Array(size)
  crypto.getRandomValues(bytes)
  return toBase64Url(bytes)
}

export const createCodeChallenge = async (verifier: string): Promise<string> => {
  const encodedVerifier = new TextEncoder().encode(verifier)
  const digest = await crypto.subtle.digest('SHA-256', encodedVerifier)
  return toBase64Url(new Uint8Array(digest))
}
