import {DependencyList,useEffect,useRef} from 'react'

export const TYPEAHEAD_DELAY_MS=300
export const TYPEAHEAD_MIN_LENGTH=2

export function rankMatches<T>(items:T[],query:string,values:(item:T)=>Array<string|undefined|null>):T[]{
  const term=query.trim().toLocaleLowerCase()
  if(!term)return items
  const score=(item:T)=>{const fields=values(item).filter(Boolean).map(x=>String(x).toLocaleLowerCase());return fields.some(x=>x===term)?0:fields.some(x=>x.startsWith(term))?1:fields.some(x=>x.includes(term))?2:3}
  return items.map((item,index)=>({item,index,score:score(item)})).filter(x=>x.score<3).sort((a,b)=>a.score-b.score||a.index-b.index).map(x=>x.item)
}

export function useTypeahead<T>(query:string,load:(query:string)=>Promise<T>,apply:(value:T)=>void,dependencies:DependencyList=[]){
  const loadRef=useRef(load),applyRef=useRef(apply),sequence=useRef(0)
  loadRef.current=load;applyRef.current=apply
  useEffect(()=>{const term=query.trim();if(term.length>0&&term.length<TYPEAHEAD_MIN_LENGTH)return
    const request=++sequence.current,timer=window.setTimeout(()=>{void loadRef.current(term).then(value=>{if(request===sequence.current)applyRef.current(value)}).catch(()=>{})},TYPEAHEAD_DELAY_MS)
    return()=>{window.clearTimeout(timer);sequence.current++}
  },[query,...dependencies])
}
