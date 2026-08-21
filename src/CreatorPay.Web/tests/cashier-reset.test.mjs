import assert from 'node:assert/strict'
import {readFileSync} from 'node:fs'
import test from 'node:test'

const source=readFileSync(new URL('../src/CashierCheckoutWorkspace.tsx',import.meta.url),'utf8')

test('Cashier success resets to a fresh purchase form after a short result message',()=>{
  for(const field of ['setCreatorCode("")','setShopperPhoneNumber("")','setPurchaseAmount("")','setResult(undefined)'])assert.ok(source.includes(field),field)
  assert.match(source,/submissionKey\.current\s*=\s*crypto\.randomUUID\(\)/)
  assert.match(source,/Purchase submitted — awaiting Customer confirmation\./)
  assert.match(source,/setTimeout\(resetEntryForm, 3000\)/)
})

test('Cashier validation and API failures use the same delayed reset path',()=>{
  assert.match(source,/showResultThenReset\(\s*eligibility\.message/s)
  assert.match(source,/showResultThenReset\(\s*\(error as Error\)\.message/s)
  assert.match(source,/disabled=\{busy\}/)
})
