import { describe, expect, it } from 'vitest'
import { formatBytes } from './filesize'

describe('formatBytes', () => {
  it('counts bytes whole and everything above them to one decimal', () => {
    expect(formatBytes(0)).toBe('0 B')
    expect(formatBytes(842)).toBe('842 B')
    expect(formatBytes(1024)).toBe('1.0 KB')
    expect(formatBytes(20_480)).toBe('20.0 KB')
    expect(formatBytes(10 * 1024 * 1024)).toBe('10.0 MB')
  })

  it('stops at gigabytes, which is well past the upload cap', () => {
    expect(formatBytes(5 * 1024 ** 3)).toBe('5.0 GB')
  })

  it('says nothing rather than something wrong about a value it cannot read', () => {
    expect(formatBytes(-1)).toBe('—')
    expect(formatBytes(Number.NaN)).toBe('—')
  })
})
