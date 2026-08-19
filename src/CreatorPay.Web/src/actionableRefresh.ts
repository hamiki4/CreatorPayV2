const eventName = 'weymela:actionable-refresh'

export function requestActionableRefresh(){
  window.dispatchEvent(new Event(eventName))
}

export function onActionableRefresh(handler:()=>void){
  window.addEventListener(eventName,handler)
  return ()=>window.removeEventListener(eventName,handler)
}
