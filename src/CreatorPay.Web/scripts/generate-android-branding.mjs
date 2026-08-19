import { chromium } from 'playwright'
import { mkdir, writeFile } from 'node:fs/promises'
import path from 'node:path'

const brandGreen = '#153c27'
const source = path.resolve('resources/weymela-mobile-logo.png')
const output = path.resolve('resources')

await mkdir(output, { recursive: true })
const browser = await chromium.launch({ headless: true })
try {
  const page = await browser.newPage()
  await page.goto(`file://${source}`)
  const sourceSize = await page.locator('img').evaluate(image => ({
    width: image.naturalWidth,
    height: image.naturalHeight,
  }))
  if (sourceSize.width < 1024 || sourceSize.height < 1024) {
    throw new Error(`Approved mobile logo must be at least 1024x1024; received ${sourceSize.width}x${sourceSize.height}.`)
  }

  async function render(name, width, height, markWidth, background = null) {
    const dataUrl = await page.locator('img').evaluate((image, options) => {
      const sourceCanvas = document.createElement('canvas')
      sourceCanvas.width = image.naturalWidth
      sourceCanvas.height = image.naturalHeight
      const sourceContext = sourceCanvas.getContext('2d', { willReadFrequently: true })
      sourceContext.drawImage(image, 0, 0)
      const pixels = sourceContext.getImageData(0, 0, sourceCanvas.width, sourceCanvas.height).data
      let left = sourceCanvas.width, top = sourceCanvas.height, right = -1, bottom = -1
      for (let y = 0; y < sourceCanvas.height; y++) for (let x = 0; x < sourceCanvas.width; x++) {
        if (pixels[(y * sourceCanvas.width + x) * 4 + 3] === 0) continue
        left = Math.min(left, x); top = Math.min(top, y); right = Math.max(right, x); bottom = Math.max(bottom, y)
      }
      if (right < left || bottom < top) throw new Error('Approved mobile logo has no visible pixels.')
      const visibleWidth = right - left + 1, visibleHeight = bottom - top + 1
      const scale = options.markWidth / visibleWidth
      const markHeight = visibleHeight * scale
      const canvas = document.createElement('canvas')
      canvas.width = options.width; canvas.height = options.height
      const context = canvas.getContext('2d')
      context.imageSmoothingEnabled = true; context.imageSmoothingQuality = 'high'
      if (options.background) { context.fillStyle = options.background; context.fillRect(0, 0, canvas.width, canvas.height) }
      context.drawImage(sourceCanvas, left, top, visibleWidth, visibleHeight,
        (canvas.width - options.markWidth) / 2, (canvas.height - markHeight) / 2, options.markWidth, markHeight)
      return canvas.toDataURL('image/png')
    }, { width, height, markWidth, background })
    await writeFile(path.join(output, name), Buffer.from(dataUrl.split(',')[1], 'base64'))
  }

  await render('icon-foreground.png', 1024, 1024, 672)
  await render('adaptive-foreground-432.png', 432, 432, 288)
  await render('icon-only.png', 1024, 1024, 672, brandGreen)
  await render('launcher-master.png', 1024, 1024, 672, brandGreen)
  await render('icon-background.png', 1024, 1024, 0, brandGreen)
  await render('splash.png', 2732, 2732, 640, brandGreen)
  for (const [density, size] of [['ldpi', 81], ['mdpi', 108], ['hdpi', 162], ['xhdpi', 216], ['xxhdpi', 324], ['xxxhdpi', 432]]) {
    await render(`../android/app/src/main/res/mipmap-${density}/ic_launcher_foreground.png`, size, size, size * 2 / 3)
  }
  console.log(`Generated Android branding from ${sourceSize.width}x${sourceSize.height} approved artwork without changing its geometry or colors.`)
} finally {
  await browser.close()
}
