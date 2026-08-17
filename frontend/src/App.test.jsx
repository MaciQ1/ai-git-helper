import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App.jsx'

describe('App', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
  })

  it('sends the access key and diff and displays the result', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValue({
      ok: true,
      status: 200,
      text: async () => JSON.stringify({
        commitMessage: 'feat: add tests',
        pullRequestDescription: '### Summary\n- Added tests',
      }),
    })
    render(<App />)

    fireEvent.change(screen.getByLabelText('Klucz dostępu'), {
      target: { value: 'test-key' },
    })
    fireEvent.change(screen.getByLabelText('Git diff'), {
      target: { value: 'diff --git a/file b/file' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Generuj commit i opis PR' }))

    await waitFor(() => expect(screen.getByText('feat: add tests')).toBeInTheDocument())
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/generate-commit',
      expect.objectContaining({
        headers: {
          'Content-Type': 'application/json',
          'X-API-Key': 'test-key',
        },
        body: JSON.stringify({ gitDiff: 'diff --git a/file b/file' }),
      }),
    )

    fireEvent.click(screen.getByRole('button', { name: 'Wyczyść diff i wynik' }))

    expect(screen.getByLabelText('Git diff')).toHaveValue('')
    expect(screen.queryByText('feat: add tests')).not.toBeInTheDocument()
  })

  it('shows an authentication error for an invalid access key', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue({
      ok: false,
      status: 401,
      text: async () => '',
    })
    render(<App />)

    fireEvent.change(screen.getByLabelText('Klucz dostępu'), {
      target: { value: 'wrong-key' },
    })
    fireEvent.change(screen.getByLabelText('Git diff'), {
      target: { value: 'diff' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Generuj commit i opis PR' }))

    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent('Nieprawidłowy klucz dostępu.'))
  })
})
