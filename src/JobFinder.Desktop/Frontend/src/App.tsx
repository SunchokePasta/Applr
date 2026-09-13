import { useCallback, useEffect, useRef, useState } from "react";

export default function App() {
  const [showIntro, setShowIntro] = useState(true);
  const [isLoading, setIsLoading] = useState(true);
  const [jobs, setJobs] = useState<DbJobDto[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const loadingRef = useRef(false);

  const loadJobs = useCallback(() => {
    if (loadingRef.current) {
      return;
    }

    loadingRef.current = true;
    setIsLoading(true);
    setError(null);
    setJobs(null);
    window.chrome?.webview?.postMessage({ type: "loadJobs" });
  }, []);

  useEffect(() => {
    const onMessage = (event: MessageEvent<WebViewMessage>) => {
      if (event.data.type === "jobsLoaded") {
        loadingRef.current = false;
        setJobs(event.data.jobs ?? []);
        setIsLoading(false);
        setShowIntro(false);
      }

      if (event.data.type === "jobsFailed") {
        loadingRef.current = false;
        setError(event.data.message ?? "Jobs could not be loaded. Please try again.");
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
        {error && <LoadingPanel message={error} isBusy={false} />}
        {jobs && <JobsTable jobs={jobs} />}
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

function JobsTable({ jobs }: { jobs: DbJobDto[] }) {
  return (
    <section className="jobs-section" aria-labelledby="jobs-heading">
      <h2 id="jobs-heading">Jobs</h2>
      <div className="table-wrap">
        <table>
          <thead><tr><th>Company</th><th>Role</th><th>Status</th><th>Posted</th><th>Closes</th><th>Visa</th></tr></thead>
          <tbody>
            {jobs.length === 0 ? (
              <tr className="empty-row"><td colSpan={6}>No jobs were returned.</td></tr>
            ) : jobs.map((job, index) => (
              <tr key={`${job.jobUrl ?? job.jobTitle}-${index}`}>
                <LinkedCell value={job.companyName} url={job.companyUrl} />
                <LinkedCell value={job.jobTitle} url={job.jobUrl} />
                <Cell value={job.status} />
                <Cell value={job.postedDateRaw} />
                <Cell value={job.closeDateRaw} />
                <Cell value={job.visaSponsorship ? "Available" : "Not listed"} />
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
