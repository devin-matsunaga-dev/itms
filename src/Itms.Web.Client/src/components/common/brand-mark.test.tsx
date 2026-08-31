import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { BrandMark } from '@/components/common/brand-mark'
// Vite's `?raw` rather than node:fs, so this test needs no Node types and the app
// tsconfig stays browser-only.
import indexHtml from '../../../index.html?raw'

const markPath = '/brand-mark.png'

describe('BrandMark', () => {
  it('renders the mark from the public path', () => {
    render(<BrandMark />)

    const mark = screen.getByRole('presentation')
    expect(mark).toHaveAttribute('src', markPath)
  })

  it("is decorative, because every place it renders names the organisation beside it", () => {
    render(<BrandMark />)

    // An accessible name here would have a screen reader announce the product twice.
    expect(screen.queryByRole('img')).not.toBeInTheDocument()
    expect(screen.getByRole('presentation')).toHaveAttribute('alt', '')
  })

  it('uses the same asset the browser tab does', () => {
    // The mark and the favicon are one file at one stable path. Renaming the asset
    // without updating index.html would leave a broken tab icon that nothing else
    // would notice.
    expect(indexHtml).toContain(`href="${markPath}"`)
    expect(indexHtml).toContain('rel="apple-touch-icon"')
    expect(indexHtml).toContain('/favicon-32.png')
  })
})
