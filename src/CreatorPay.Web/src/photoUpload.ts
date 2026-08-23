import { apiBase } from './apiClient'

export const profilePhotoAccept = 'image/jpeg,image/png,image/webp,image/heic,image/heif'
export const profilePhotoMaxUploadBytes = 12 * 1024 * 1024
export const profilePhotoMaxDimension = 1600
export const profilePhotoUnsupportedMessage = 'Please choose a JPG, PNG, or WebP image.'
export const profilePhotoProcessingMessage = "We couldn't process this photo. Please choose another photo."

const processableMimeTypes = new Set([
  'image/jpeg',
  'image/jpg',
  'image/png',
  'image/webp',
  'image/heic',
  'image/heif',
])

const processableExtensions = new Set(['jpg', 'jpeg', 'png', 'webp', 'heic', 'heif'])

export function isSupportedProfilePhoto(file: Pick<File, 'name' | 'type'>) {
  const type = file.type.toLowerCase()
  if (type && processableMimeTypes.has(type)) return true
  const extension = file.name.split('.').pop()?.toLowerCase() ?? ''
  return processableExtensions.has(extension)
}

export function buildProfilePhotoUrl(publicCreatorId: string, version?: string) {
  const path = `/api/v1/discovery/creators/${encodeURIComponent(publicCreatorId)}/photo${version ? `?v=${encodeURIComponent(version)}` : ''}`
  return apiBase ? `${apiBase}${path}` : path
}

export function normalizeProfilePhotoUrl(photoUrl?: string) {
  if (!photoUrl) return undefined
  if (/^https?:\/\//i.test(photoUrl)) return photoUrl
  if (!photoUrl.startsWith('/')) return photoUrl
  return apiBase ? `${apiBase}${photoUrl}` : photoUrl
}

function toBlob(canvas: HTMLCanvasElement, type: string, quality?: number) {
  return new Promise<Blob>((resolve, reject) => {
    canvas.toBlob((blob) => {
      if (blob) resolve(blob)
      else reject(new Error('Canvas conversion failed.'))
    }, type, quality)
  })
}

async function decodeImage(file: File) {
  if ('createImageBitmap' in window) {
    try {
      return await createImageBitmap(file, { imageOrientation: 'from-image' as ImageOrientation })
    } catch {
      // Fall back to an <img> decode path when the browser cannot bitmap-decode this format.
    }
  }

  const url = URL.createObjectURL(file)
  try {
    const image = await new Promise<HTMLImageElement>((resolve, reject) => {
      const element = new Image()
      element.onload = () => resolve(element)
      element.onerror = () => reject(new Error('Image decode failed.'))
      element.src = url
    })
    return image
  } finally {
    URL.revokeObjectURL(url)
  }
}

function drawToCanvas(source: ImageBitmap | HTMLImageElement, maxDimension: number) {
  const width = source.width
  const height = source.height
  const scale = Math.min(1, maxDimension / Math.max(width, height))
  const outputWidth = Math.max(1, Math.round(width * scale))
  const outputHeight = Math.max(1, Math.round(height * scale))
  const canvas = document.createElement('canvas')
  canvas.width = outputWidth
  canvas.height = outputHeight
  const context = canvas.getContext('2d')
  if (!context) throw new Error('Canvas context unavailable.')
  context.imageSmoothingEnabled = true
  context.imageSmoothingQuality = 'high'
  context.drawImage(source, 0, 0, outputWidth, outputHeight)
  return canvas
}

async function encodePreparedPhoto(source: ImageBitmap | HTMLImageElement, maxDimension: number, quality: number) {
  const canvas = drawToCanvas(source, maxDimension)
  const blob = await toBlob(canvas, 'image/jpeg', quality)
  return blob
}

export async function prepareProfilePhoto(file: File) {
  if (!isSupportedProfilePhoto(file)) {
    throw new Error(profilePhotoUnsupportedMessage)
  }

  const source = await decodeImage(file)
  const attempts = [
    { maxDimension: profilePhotoMaxDimension, quality: 0.9 },
    { maxDimension: 1280, quality: 0.84 },
    { maxDimension: 1024, quality: 0.8 },
    { maxDimension: 768, quality: 0.78 },
  ]

  for (const attempt of attempts) {
    try {
      const blob = await encodePreparedPhoto(source, attempt.maxDimension, attempt.quality)
      if (blob.size > profilePhotoMaxUploadBytes) continue
      if ('close' in source && typeof source.close === 'function') source.close()
      return new File([blob], `${file.name.replace(/\.[^.]+$/, '') || 'profile-photo'}.jpg`, {
        type: 'image/jpeg',
        lastModified: Date.now(),
      })
    } catch {
      // Try the next smaller normalization step.
    }
  }

  if ('close' in source && typeof source.close === 'function') source.close()
  throw new Error(profilePhotoProcessingMessage)
}
