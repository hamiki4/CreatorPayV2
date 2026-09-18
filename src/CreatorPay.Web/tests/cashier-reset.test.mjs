import assert from 'node:assert/strict'
import {readFileSync} from 'node:fs'
import test from 'node:test'

const source=readFileSync(new URL('../src/CashierCheckoutWorkspace.tsx',import.meta.url),'utf8')

test('Cashier success stays visible until Next Customer resets sensitive checkout state',()=>{
  assert.match(source,/setMessage\('Sale completed\.'\)/)
  assert.match(source,/function next\(\)\{setSale\(undefined\);setOffer\(undefined\);setToken\(''\);setAmount\(''\);setMessage\(''\)\}/)
  assert.match(source,/Next Customer/)
  assert.doesNotMatch(source,/Creator ID|Customer Phone Number/)
})

test('Cashier validation and API failures remain visible without consuming or clearing the QR',()=>{
  assert.match(source,/catch\(error\)\{setOffer\(undefined\);setMessage\(\(error as Error\)\.message\)\}/)
  assert.match(source,/catch\(error\)\{setMessage\(\(error as Error\)\.message\)\}/)
  assert.match(source,/disabled=\{busy\|\|!token\.trim\(\)\}/)
  assert.match(source,/disabled=\{busy\|\|!amount\}/)
})
