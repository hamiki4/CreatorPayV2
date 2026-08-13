import { chromium } from 'playwright'
import { mkdir, writeFile } from 'node:fs/promises'
import path from 'node:path'

const source = path.resolve('public/weymela-logo.png')
const output = path.resolve('public/icons')
const sizes = [16, 32, 48, 180, 192, 512]

await mkdir(output, { recursive: true })
const browser = await chromium.launch({ headless: true })
try {
  const page = await browser.newPage()
  await page.goto(`file://${source}`)
  const sourceSize = await page.locator('img').evaluate(image => ({
    width: image.naturalWidth,
    height: image.naturalHeight,
  }))
  if (sourceSize.width !== sourceSize.height) {
    throw new Error(`Master logo must be square; received ${sourceSize.width}x${sourceSize.height}`)
  }
  const generated = new Map()
  for (const size of sizes) {
    const dataUrl = await page.locator('img').evaluate((image, targetSize) => {
      const canvas = document.createElement('canvas')
      canvas.width = targetSize
      canvas.height = targetSize
      const context = canvas.getContext('2d')
      context.imageSmoothingEnabled = true
      context.imageSmoothingQuality = 'high'
      context.drawImage(image, 0, 0, targetSize, targetSize)
      return canvas.toDataURL('image/png')
    }, size)
    const png = Buffer.from(dataUrl.split(',')[1], 'base64')
    generated.set(size, png)
    await writeFile(path.join(output, `weymela-${size}x${size}.png`), png)
  }
  const faviconSizes = [16, 32, 48]
  const header = Buffer.alloc(6 + faviconSizes.length * 16)
  header.writeUInt16LE(0, 0)
  header.writeUInt16LE(1, 2)
  header.writeUInt16LE(faviconSizes.length, 4)
  let offset = header.length
  faviconSizes.forEach((size, index) => {
    const png = generated.get(size)
    const entry = 6 + index * 16
    header.writeUInt8(size, entry)
    header.writeUInt8(size, entry + 1)
    header.writeUInt8(0, entry + 2)
    header.writeUInt8(0, entry + 3)
    header.writeUInt16LE(1, entry + 4)
    header.writeUInt16LE(32, entry + 6)
    header.writeUInt32LE(png.length, entry + 8)
    header.writeUInt32LE(offset, entry + 12)
    offset += png.length
  })
  await writeFile(path.resolve('public/favicon.ico'), Buffer.concat([header, ...faviconSizes.map(size => generated.get(size))]))
  console.log(`Generated ${sizes.length} icons from ${sourceSize.width}x${sourceSize.height} master artwork.`)
} finally {
  await browser.close()
}
