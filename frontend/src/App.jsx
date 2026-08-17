import { useState } from 'react'

const API_URL = import.meta.env.VITE_API_URL || ''
const MAX_DIFF_LENGTH = 200_000

export default function App() {
  const [gitDiff, setGitDiff] = useState('')
  const [accessKey, setAccessKey] = useState('')
  const [result, setResult] = useState(null)
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)
  const [copied, setCopied] = useState('')

  async function generate(event) {
    event.preventDefault()
    setLoading(true)
    setError('')
    setResult(null)
    setCopied('')

    const controller = new AbortController()
    const timeout = setTimeout(() => controller.abort(), 65_000)

    try {
      const response = await fetch(`${API_URL}/api/generate-commit`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'X-API-Key': accessKey.trim(),
        },
        body: JSON.stringify({ gitDiff }),
        signal: controller.signal,
      })

      const body = await response.text()
      let data
      try {
        data = body ? JSON.parse(body) : null
      } catch {
        throw new Error('Backend zwrócił niepoprawną odpowiedź.')
      }

      if (!response.ok) {
        if (response.status === 401) {
          throw new Error('Nieprawidłowy klucz dostępu.')
        }
        throw new Error(data?.error || data?.detail || 'Nie udało się wygenerować wyniku.')
      }

      if (!data?.commitMessage || !data?.pullRequestDescription) {
        throw new Error('Backend zwrócił niepełny wynik.')
      }

      setResult(data)
    } catch (requestError) {
      if (requestError instanceof DOMException && requestError.name === 'AbortError') {
        setError('Żądanie trwało zbyt długo. Spróbuj ponownie.')
      } else {
        setError(requestError instanceof Error ? requestError.message : 'Wystąpił nieznany błąd.')
      }
    } finally {
      clearTimeout(timeout)
      setLoading(false)
    }
  }

  async function copyResult(name, value) {
    try {
      await navigator.clipboard.writeText(value)
      setCopied(name)
      setTimeout(() => setCopied(''), 2_000)
    } catch {
      setError('Nie udało się skopiować wyniku do schowka.')
    }
  }

  function clearForm() {
    setGitDiff('')
    setResult(null)
    setError('')
    setCopied('')
  }

  return (
    <main className="min-h-screen px-5 py-10 sm:px-10">
      <div className="mx-auto max-w-5xl">
        <header className="mb-10 flex items-end justify-between gap-4">
          <div>
            <p className="mb-3 font-mono text-xs uppercase tracking-[0.3em] text-cyan-300">AI Git Helper</p>
            <h1 className="text-4xl font-semibold tracking-tight text-white sm:text-6xl">Z diffu do dobrego PR.</h1>
          </div>
          <span className="hidden rounded-full border border-white/10 px-3 py-1 font-mono text-xs text-slate-400 sm:block">.NET 8 / React</span>
        </header>

        <section className="grid gap-6 lg:grid-cols-[1.1fr_0.9fr]">
          <form className="panel" onSubmit={generate}>
            <div className="mb-6">
              <label htmlFor="access-key" className="mb-2 block font-medium text-white">Klucz dostępu</label>
              <input
                id="access-key"
                type="password"
                value={accessKey}
                onChange={(event) => setAccessKey(event.target.value)}
                autoComplete="off"
                placeholder="Wpisz klucz skonfigurowany na backendzie"
                className="w-full rounded-lg border border-white/10 bg-[#0b1220] p-3 font-mono text-sm text-slate-200 outline-none transition placeholder:text-slate-600 focus:border-cyan-400"
              />
              <p className="mt-2 text-xs text-slate-500">Klucz jest używany tylko w tej sesji i nie jest zapisywany w przeglądarce.</p>
            </div>
            <div className="mb-5 flex items-center justify-between">
              <label htmlFor="diff" className="font-medium text-white">Git diff</label>
              <span className="font-mono text-xs text-slate-500">{gitDiff.length.toLocaleString()} / {MAX_DIFF_LENGTH.toLocaleString()}</span>
            </div>
            <textarea
              id="diff"
              value={gitDiff}
              maxLength={MAX_DIFF_LENGTH}
              onChange={(event) => setGitDiff(event.target.value)}
              placeholder={'git diff --staged\n\nWklej tutaj zmiany, które chcesz opisać...'}
              className="h-80 w-full resize-none rounded-lg border border-white/10 bg-[#0b1220] p-4 font-mono text-sm leading-6 text-slate-200 outline-none transition placeholder:text-slate-600 focus:border-cyan-400"
            />
            <button
              type="submit"
              disabled={loading || !accessKey.trim() || !gitDiff.trim()}
              className="mt-4 w-full rounded-lg bg-cyan-300 px-5 py-3 font-semibold text-slate-950 transition hover:bg-cyan-200 disabled:cursor-not-allowed disabled:opacity-40"
            >
              {loading ? 'Analizuję zmiany...' : 'Generuj commit i opis PR'}
            </button>
            <div className="mt-3 flex justify-end">
              <button
                type="button"
                onClick={clearForm}
                disabled={loading || (!gitDiff && !result && !error)}
                className="rounded-lg border border-white/10 px-4 py-2 text-sm text-slate-400 transition hover:border-cyan-400 hover:text-cyan-300 disabled:cursor-not-allowed disabled:opacity-40"
              >
                Wyczyść diff i wynik
              </button>
            </div>
            {error && <p role="alert" className="mt-4 rounded-lg border border-red-400/30 bg-red-400/10 p-3 text-sm text-red-200">{error}</p>}
          </form>

          <div className="panel min-h-[28rem]" aria-busy={loading}>
            <p className="mb-6 font-mono text-xs uppercase tracking-[0.2em] text-slate-500">Wynik</p>
            {result ? (
              <div className="space-y-6" aria-live="polite">
                <ResultBlock
                  label="Commit message"
                  value={result.commitMessage}
                  copied={copied === 'commit'}
                  onCopy={() => copyResult('commit', result.commitMessage)}
                  mono
                />
                <ResultBlock
                  label="Pull request description"
                  value={result.pullRequestDescription}
                  copied={copied === 'pr'}
                  onCopy={() => copyResult('pr', result.pullRequestDescription)}
                />
              </div>
            ) : (
              <div className="flex h-80 items-center justify-center text-center text-slate-600" role="status">
                <p>{loading ? 'Model analizuje zmiany...' : <>Wynik wygenerowany przez AI<br />pojawi się tutaj.</>}</p>
              </div>
            )}
          </div>
        </section>
      </div>
    </main>
  )
}

function ResultBlock({ label, value, copied, onCopy, mono = false }) {
  return (
    <div>
      <div className="mb-2 flex items-center justify-between gap-3">
        <h2 className="label mb-0">{label}</h2>
        <button type="button" onClick={onCopy} className="copy-button">{copied ? 'Skopiowano' : 'Kopiuj'}</button>
      </div>
      <div className={`result-box whitespace-pre-wrap text-sm leading-6 ${mono ? 'font-mono' : ''}`}>{value}</div>
    </div>
  )
}
