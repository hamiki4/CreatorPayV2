import { readFile, writeFile } from 'node:fs/promises'

export function resolveApiOrigin(apiUrl) {
  const value = String(apiUrl ?? '').trim()
  if (!value || value === '/') return ''
  try {
    const url = new URL(value)
    return url.protocol === 'http:' || url.protocol === 'https:' ? url.origin : ''
  } catch {
    return ''
  }
}

export function renderNginxConfig(template, apiUrl) {
  const origin = resolveApiOrigin(apiUrl)
  const imgSrc = origin ? `'self' data: ${origin}` : `'self' data:`
  if (!template.includes('__IMG_SRC__')) throw new Error('Nginx template is missing the __IMG_SRC__ placeholder.')
  return template.replace('__IMG_SRC__', imgSrc)
}

async function main(argv) {
  const args = new Map()
  for (let index = 2; index < argv.length; index += 2) {
    const key = argv[index]
    const value = argv[index + 1]
    if (!key?.startsWith('--') || value === undefined) throw new Error('Expected --template, --output, and --api-url arguments.')
    args.set(key, value)
  }
  const templatePath = args.get('--template')
  const outputPath = args.get('--output')
  const apiUrl = args.get('--api-url') ?? ''
  if (!templatePath || !outputPath) throw new Error('Expected --template, --output, and --api-url arguments.')
  const template = await readFile(templatePath, 'utf8')
  const rendered = renderNginxConfig(template, apiUrl)
  await writeFile(outputPath, rendered)
}

if (import.meta.url === `file://${process.argv[1]}`) {
  main(process.argv).catch(error => {
    console.error(error instanceof Error ? error.message : String(error))
    process.exit(1)
  })
}
