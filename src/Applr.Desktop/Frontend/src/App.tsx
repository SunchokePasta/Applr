import { useCallback, useEffect, useRef, useState } from "react";

type LoadError = {
  message: string;
  reference?: string;
};

export default function App() {
  const [showIntro, setShowIntro] = useState(true);
  const [isLoading, setIsLoading] = useState(true);
  const [existingJobs, setExistingJobs] = useState<JobDto[] | null>(null);
  const [newJobs, setNewJobs] = useState<JobDto[] | null>(null);
  const [error, setError] = useState<LoadError | null>(null);
  const loadingRef = useRef(false);

  const loadJobs = useCallback(() => {
    if (loadingRef.current) {
      return;
    }

    loadingRef.current = true;
    setIsLoading(true);
    setError(null);
    setExistingJobs(null);
    setNewJobs(null);
    window.chrome?.webview?.postMessage({ type: "loadJobs" });
  }, []);

  useEffect(() => {
    const onMessage = (event: MessageEvent<WebViewMessage>) => {
      if (event.data.type === "jobsLoaded") {
        loadingRef.current = false;
        setExistingJobs(event.data.existingJobs ?? []);
        setNewJobs(event.data.newJobs ?? []);
        setIsLoading(false);
        setShowIntro(false);
      }

      if (event.data.type === "jobsFailed") {
        loadingRef.current = false;
        // The message arrives already phrased for a person -- it was
        // written by whichever tier actually failed (Applr.RestApi,
        // Applr.API or the desktop app) and passed down unchanged. The
        // reference beside it is the same id on the matching log line.
        setError({
          message: event.data.message ?? "Jobs could not be loaded. Please try again.",
          reference: event.data.reference,
        });
        setIsLoading(false);
        setShowIntro(false);
      }
    };

    window.chrome?.webview?.addEventListener("message", onMessage);
    loadJobs();

    return () => {
      window.chrome?.webview?.removeEventListener("message", onMessage);
    };
  }, [loadJobs]);

  return (
    <>
      <IntroScreen isVisible={showIntro} />
      <main className={`app-shell ${showIntro ? "" : "is-visible"}`} aria-hidden={showIntro}>
        <header className="page-header">
          <div>
            <p className="eyebrow">APPLICATION TRACKER</p>
            <h1>Applr</h1>
            <p className="subtitle">Review your latest tracked opportunities.</p>
          </div>
          <button type="button" onClick={loadJobs} disabled={isLoading}>
            Load jobs
          </button>
        </header>

        {isLoading && <LoadingPanel />}
        {error && <ErrorPanel error={error} onRetry={loadJobs} />}

        {/*
          Top table: New jobs (status "New", from GET jobs/new). Shown
          whenever a load has completed, even at 0 -- while we're
          chasing the "nothing's showing" bug, a visible "New (0)" tells
          you the call returned an empty array, versus this section not
          rendering at all telling you nothing about why.
        */}
        {newJobs && (
          <JobsTable
            headingId="new-jobs-heading"
            title={`New (${newJobs.length})`}
            jobs={newJobs}
            emptyMessage="No new jobs were returned."
          />
        )}

        {/*
          Bottom table: existing jobs (status "Unreviewed", from GET
          jobs/existing) -- always there once a load has happened.
        */}
        {existingJobs && (
          <JobsTable
            headingId="existing-jobs-heading"
            title={`Existing (${existingJobs.length})`}
            jobs={existingJobs}
            emptyMessage="No jobs were returned."
          />
        )}
      </main>
    </>
  );
}

function IntroScreen({ isVisible }: { isVisible: boolean }) {
  return (
    <section className={`intro-screen ${isVisible ? "" : "is-hidden"}`} aria-label="Applr is opening">
      <h1 className="wordmark" aria-label="Applr">
        {"Applr".split("").map((letter, index) => (
          <span aria-hidden="true" style={{ "--letter-delay": `${index * 120}ms` } as React.CSSProperties} key={`${letter}-${index}`}>
            {letter}
          </span>
        ))}
      </h1>
      <div className="intro-dots" aria-hidden="true">
        <span />
        <span />
        <span />
      </div>
    </section>
  );
}

function LoadingPanel({ message = "Jobs loading", isBusy = true }: { message?: string; isBusy?: boolean }) {
  return (
    <section className="loading-panel" aria-live="polite" aria-busy={isBusy}>
      <p>{message}</p>
      {isBusy && <div className="loading-track" aria-hidden="true"><span /></div>}
    </section>
  );
}

/**
 * Its own component rather than a reused LoadingPanel: a failure needs
 * role="alert", a retry affordance, and somewhere to put the reference
 * id -- none of which belong on a spinner.
 */
function ErrorPanel({ error, onRetry }: { error: LoadError; onRetry: () => void }) {
  return (
    <section className="error-panel" role="alert">
      <p className="error-message">{error.message}</p>
      {error.reference && (
        <p className="error-reference">
          Reference: <code>{error.reference}</code>
        </p>
      )}
      <button type="button" onClick={onRetry}>
        Try again
      </button>
    </section>
  );
}

function JobsTable({
  headingId,
  title,
  jobs,
  emptyMessage,
}: {
  headingId: string;
  title: string;
  jobs: JobDto[];
  emptyMessage: string;
}) {
  return (
    <section className="jobs-section" aria-labelledby={headingId}>
      <h2 id={headingId}>{title}</h2>
      <div className="table-wrap">
        <table>
          <thead><tr><th>Company</th><th>Role</th><th>Status</th><th>Posted</th><th>Closes</th></tr></thead>
          <tbody>
            {jobs.length === 0 ? (
              <tr className="empty-row"><td colSpan={5}>{emptyMessage}</td></tr>
            ) : jobs.map((job) => (
              <tr key={job.id}>
                <Cell value={job.companyName} />
                <LinkedCell value={job.jobTitle} url={job.jobUrl} />
                <Cell value={job.status} />
                <Cell value={job.postedDate} />
                <Cell value={job.closeDate} />
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}

function Cell({ value }: { value?: string }) {
  return <td>{value || "—"}</td>;
}

function LinkedCell({ value, url }: { value?: string; url?: string }) {
  return <td>{url ? <a href={url} target="_blank">{value || url}</a> : value || "—"}</td>;
}
