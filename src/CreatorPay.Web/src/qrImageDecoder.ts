import jsQR from 'jsqr'

type ImageSource=ImageBitmap|HTMLImageElement
const maximumSides=[2400,1800,1200]

async function loadHtmlImage(file:File):Promise<{source:HTMLImageElement;width:number;height:number;dispose:()=>void}>{
  const url=URL.createObjectURL(file),image=new Image()
  try{
    await new Promise<void>((resolve,reject)=>{image.onload=()=>resolve();image.onerror=()=>reject(new Error('Image load failed.'));image.src=url})
    return{source:image,width:image.naturalWidth,height:image.naturalHeight,dispose:()=>URL.revokeObjectURL(url)}
  }catch(error){URL.revokeObjectURL(url);throw error}
}

async function loadImage(file:File):Promise<{source:ImageSource;width:number;height:number;dispose:()=>void}>{
  if('createImageBitmap' in window){
    try{
      const bitmap=await createImageBitmap(file,{imageOrientation:'from-image'})
      return{source:bitmap,width:bitmap.width,height:bitmap.height,dispose:()=>bitmap.close()}
    }catch{
      // Safari can display camera formats that createImageBitmap cannot decode.
      return loadHtmlImage(file)
    }
  }
  return loadHtmlImage(file)
}

function pixels(source:ImageSource,width:number,height:number,rotation:number){
  const swap=rotation%180!==0,canvas=document.createElement('canvas')
  canvas.width=swap?height:width;canvas.height=swap?width:height
  const context=canvas.getContext('2d',{willReadFrequently:true})
  if(!context)throw new Error('Canvas unavailable.')
  context.translate(canvas.width/2,canvas.height/2)
  context.rotate(rotation*Math.PI/180)
  context.drawImage(source,-width/2,-height/2,width,height)
  return context.getImageData(0,0,canvas.width,canvas.height)
}

function contrastGrayscale(data:ImageData){
  const enhanced=new Uint8ClampedArray(data.data.length)
  let minimum=255,maximum=0
  for(let index=0;index<data.data.length;index+=4){
    const value=Math.round(.299*data.data[index]+.587*data.data[index+1]+.114*data.data[index+2])
    minimum=Math.min(minimum,value);maximum=Math.max(maximum,value)
  }
  const range=Math.max(1,maximum-minimum)
  for(let index=0;index<data.data.length;index+=4){
    const value=Math.round(.299*data.data[index]+.587*data.data[index+1]+.114*data.data[index+2])
    const adjusted=Math.max(0,Math.min(255,Math.round((value-minimum)*255/range)))
    enhanced[index]=enhanced[index+1]=enhanced[index+2]=adjusted;enhanced[index+3]=data.data[index+3]
  }
  return enhanced
}

export async function decodeQrImage(file:File):Promise<string>{
  if(!file.type.startsWith('image/')&&!/\.(heic|heif)$/i.test(file.name))throw new Error('Unsupported image.')
  const image=await loadImage(file)
  try{
    const dimensions=new Set<string>()
    for(const maximumSide of maximumSides){
      const scale=Math.min(1,maximumSide/Math.max(image.width,image.height))
      const width=Math.max(1,Math.round(image.width*scale)),height=Math.max(1,Math.round(image.height*scale)),key=`${width}x${height}`
      if(dimensions.has(key))continue
      dimensions.add(key)
      for(const rotation of [0,90,180,270]){
        const data=pixels(image.source,width,height,rotation)
        const decoded=jsQR(data.data,data.width,data.height,{inversionAttempts:'attemptBoth'})
          ??jsQR(contrastGrayscale(data),data.width,data.height,{inversionAttempts:'attemptBoth'})
        if(decoded?.data)return decoded.data
      }
    }
    throw new Error('QR not found.')
  }finally{image.dispose()}
}
