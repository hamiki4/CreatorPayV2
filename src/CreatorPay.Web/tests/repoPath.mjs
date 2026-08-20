import {existsSync} from 'node:fs'
import {dirname, resolve} from 'node:path'
import {fileURLToPath} from 'node:url'

function findRepoRoot(metaUrl) {
  const starts = [dirname(fileURLToPath(metaUrl)), process.cwd()]
  for (const start of starts) {
    let dir = start
    while (true) {
      if (existsSync(resolve(dir, 'docker-compose.yml')) && existsSync(resolve(dir, 'src/CreatorPay.Api/Admin/AdminEndpoints.cs'))) return dir
      const parent = dirname(dir)
      if (parent === dir) break
      dir = parent
    }
  }
  throw new Error(`Unable to locate repository root from ${metaUrl} or cwd ${process.cwd()}`)
}

export function repoPath(metaUrl, ...segments) {
  return resolve(findRepoRoot(metaUrl), ...segments)
}
