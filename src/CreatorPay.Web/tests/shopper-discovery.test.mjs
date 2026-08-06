import test from 'node:test'
import assert from 'node:assert/strict'
import {readFileSync} from 'node:fs'

const ui=readFileSync(new URL('../src/CustomerWorkspace.tsx',import.meta.url),'utf8')
const notifications=readFileSync(new URL('../src/NotificationWorkspace.tsx',import.meta.url),'utf8')
test('shopper money is formatted with two decimals',()=>assert.match(ui,/toFixed\(2\)/))
test('social links use safe external-link attributes',()=>{assert.match(ui,/target="_blank"/);assert.match(ui,/rel="noopener noreferrer"/)})
test('English and Amharic shopper labels are present',()=>{assert.match(ui,/Discover Creators/);assert.match(ui,/ፈጣሪዎችን ያግኙ/)})
test('shopper notification preference table and raw event labels are absent',()=>{assert.doesNotMatch(notifications,/NotificationPreferences/);assert.doesNotMatch(notifications,/x\.type\s*}.*x\.status/s)})
