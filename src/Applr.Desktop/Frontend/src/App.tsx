import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import {
  columnFilteringFeature,
  createColumnHelper,
  createCoreRowModel,
  createFilteredRowModel,
  createSortedRowModel,
  filterFn_includesString,
  flexRender,
  globalFilteringFeature,
  rowSortingFeature,
  sortFns,
  tableFeatures,
  useTable,
  type ColumnDef,
  type SortingState,
} from "@tanstack/react-table";

type LoadError = {
  message: string;
  reference?: string;
};

/**
 * Per-row state for the apply button. Keyed by job id rather than held on
 * the row, because the reply arrives as a message on the window long after
 * the click and has to find its way back to the right row.
 */
type ApplyState =
  | { status: "running" }
  | { status: "done"; filled: string[]; notFilled: string[]; unmatchedLabels: string[] }
  | { status: "failed"; message: string; reference?: string };

/**
 * How long a row waits for a reply before it gives up on its own.
 *
 * The desktop app answers every applyToJob it can read a jobId out of, so
 * this is not the ordinary path -- it is the backstop for a message that
 * never arrives at all (a WebView tearing down mid-fill, a malformed id).
 * Without it such a row sits on "Applying" forever and its own in-flight
 * guard refuses every retry, which means restarting the app.
 *
 * Comfortably past the 5 minute ApplrFiller HTTP timeout, because a fill
 * that types each character visibly is legitimately slow and a watchdog
 * that fires first would call a working run a failure.
 */
const APPLY_WATCHDOG_MS = 6 * 60 * 1000;

export default function App() {
  const [showIntro, setShowIntro] = useState(true);
  const [isLoading, setIsLoading] = useState(true);
  const [existingJobs, setExistingJobs] = useState<JobDto[] | null>(null);
  const [newJobs, setNewJobs] = useState<JobDto[] | null>(null);
  const [error, setError] = useState<LoadError | null>(null);
  const [applyStates, setApplyStates] = useState<Record<number, ApplyState>>({});
  const [selectedJobId, setSelectedJobId] = useState<number | null>(null);
  const [search, setSearch] = useState("");
  const loadingRef = useRef(false);

  /**
   * Which jobs have an apply in flight, and the watchdog timer for each.
   *
   * In refs rather than in applyStates because the guard has to be read and
   * written during an event handler, not during a render: the check used to
   * live inside the setApplyStates updater, which React may call more than
   * once for a single update. Every extra call sent another applyToJob, so
   * one click could start two fills racing over the same page.
   */
  const inFlightRef = useRef(new Set<number>());
  const watchdogsRef = useRef(new Map<number, number>());

  const clearWatchdog = useCallback((jobId: number) => {
    const timer = watchdogsRef.current.get(jobId);

    if (timer !== undefined) {
      window.clearTimeout(timer);
      watchdogsRef.current.delete(jobId);
    }

    inFlightRef.current.delete(jobId);
  }, []);

  const loadJobs = useCallback(() => {
    if (loadingRef.current) {
      return;
    }

    loadingRef.current = true;
    setIsLoading(true);
    setError(null);
    setExistingJobs(null);
    setNewJobs(null);
    setApplyStates({});
    setSelectedJobId(null);
    window.chrome?.webview?.postMessage({ type: "loadJobs" });
  }, []);

  const applyToJob = useCallback(
    (job: JobDto) => {
      // Ignore a second click while one is in flight: the fill types each
      // character visibly and takes a while, so a row looks idle long
      // after it started.
      if (inFlightRef.current.has(job.id)) {
        return;
      }

      inFlightRef.current.add(job.id);

      watchdogsRef.current.set(
        job.id,
        window.setTimeout(() => {
          watchdogsRef.current.delete(job.id);
          inFlightRef.current.delete(job.id);
          setApplyStates((previous) =>
            previous[job.id]?.status === "running"
              ? {
                  ...previous,
                  [job.id]: {
                    status: "failed",
                    message:
                      "No answer from the form filler. Check the browser, then try again.",
                  },
                }
              : previous,
          );
        }, APPLY_WATCHDOG_MS),
      );

      setSelectedJobId(job.id);
      setApplyStates((previous) => ({ ...previous, [job.id]: { status: "running" } }));

      // The url goes with the id: the desktop app opens the posting in the
      // preview pane and waits for it before filling, and passes the page it
      // landed on to ApplrFiller so the right form gets the details.
      window.chrome?.webview?.postMessage({
        type: "applyToJob",
        jobId: job.id,
        url: job.jobUrl,
      });
    },
    [],
  );

  useEffect(() => {
    const onMessage = (event: MessageEvent<WebViewMessage>) => {
      if (event.data.type === "jobsLoaded") {
        loadingRef.current = false;
        setExistingJobs(event.data.existingJobs ?? []);
        setNewJobs(event.data.newJobs ?? []);
        setIsLoading(false);
        setShowIntro(false);
      }

      if (event.data.type === "applyFinished" && event.data.jobId !== undefined) {
        const { jobId } = event.data;
        clearWatchdog(jobId);
        setApplyStates((previous) => ({
          ...previous,
          [jobId]: {
            status: "done",
            filled: event.data.filled ?? [],
            notFilled: event.data.notFilled ?? [],
            unmatchedLabels: event.data.unmatchedLabels ?? [],
          },
        }));
      }

      if (event.data.type === "applyFailed" && event.data.jobId !== undefined) {
        const { jobId } = event.data;
        clearWatchdog(jobId);
        setApplyStates((previous) => ({
          ...previous,
          [jobId]: {
            status: "failed",
            message: event.data.message ?? "The form couldn't be filled.",
            reference: event.data.reference,
          },
        }));
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
      watchdogsRef.current.forEach((timer) => window.clearTimeout(timer));
      watchdogsRef.current.clear();
      inFlightRef.current.clear();
    };
  }, [clearWatchdog, loadJobs]);

  const hasJobs = (newJobs?.length ?? 0) + (existingJobs?.length ?? 0) > 0;

  return (
    <>
      <IntroScreen isVisible={showIntro} />
      <main className={`app-shell ${showIntro ? "" : "is-visible"}`} aria-hidden={showIntro}>
        <header className="page-header">
          <h1>Applr</h1>
          <div className="header-actions">
            {/*
              One field for both tables rather than one each. The two are
              the same kind of thing split by status, so a single place to
              search is what people expect to find -- and a term that
              matches nothing above still shows its matches below.
            */}
            {hasJobs && (
              <SearchField value={search} onChange={setSearch} />
            )}
            <button type="button" onClick={loadJobs} disabled={isLoading}>
              Load jobs
            </button>
          </div>
        </header>

        {isLoading && <LoadingPanel />}
        {error && <ErrorPanel error={error} onRetry={loadJobs} />}

        {/*
          Top table: New jobs (status "New", from GET jobs/new). Rendered
          only when there is something in it -- an empty "New (0)" section
          was worth keeping while the "nothing's showing" bug was open, but
          it is now a permanent empty box above the table people came for.
        */}
        {newJobs && newJobs.length > 0 && (
          <JobsTable
            headingId="new-jobs-heading"
            title="New"
            jobs={newJobs}
            search={search}
            applyStates={applyStates}
            onApply={applyToJob}
            selectedJobId={selectedJobId}
            onSelect={setSelectedJobId}
          />
        )}

        {/*
          Bottom table: existing jobs (status "Unreviewed", from GET
          jobs/existing) -- always there once a load has happened.
        */}
        {existingJobs && (
          <JobsTable
            headingId="existing-jobs-heading"
            title="Existing"
            jobs={existingJobs}
            search={search}
            emptyMessage="No jobs were returned."
            applyStates={applyStates}
            onApply={applyToJob}
            selectedJobId={selectedJobId}
            onSelect={setSelectedJobId}
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

function SearchField({ value, onChange }: { value: string; onChange: (value: string) => void }) {
  return (
    <div className="search-field">
      <span className="search-icon" aria-hidden="true">⌕</span>
      <input
        type="search"
        value={value}
        onChange={(event) => onChange(event.target.value)}
        placeholder="Search company, role or status"
        aria-label="Search jobs by company, role or status"
      />
    </div>
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

/**
 * What the cell renderers need but the row does not carry. Passed through
 * the table's meta rather than closed over, so the column definitions stay
 * module-level constants instead of being rebuilt on every state change.
 */
type JobsTableMeta = {
  applyStates: Record<number, ApplyState>;
  onApply: (job: JobDto) => void;
};

/**
 * v9 registers features explicitly rather than shipping them all, so this is
 * the whole of what the jobs tables use: sorting, and one filter applied
 * across every column. columnFilteringFeature is here because global
 * filtering and the filtered row model are both built on it, not because
 * per-column filters are wanted -- one search field is the point.
 */
const jobTableFeatures = tableFeatures({
  columnFilteringFeature,
  globalFilteringFeature,
  rowSortingFeature,
  coreRowModel: createCoreRowModel(),
  filteredRowModel: createFilteredRowModel(),
  sortedRowModel: createSortedRowModel(),
  filterFns: { includesString: filterFn_includesString },
  sortFns,
  tableMeta: {} as JobsTableMeta,
});

const columnHelper = createColumnHelper<typeof jobTableFeatures, JobDto>();

// Annotated rather than inferred: the array mixes accessor columns (which
// carry the type of the field they read) with one display column (which
// reads nothing), and the union of those does not narrow to a single
// ColumnDef on its own.
const JOB_COLUMNS: ColumnDef<typeof jobTableFeatures, JobDto, any>[] = [
  columnHelper.accessor("companyName", {
    header: "Company",
    cell: (info) => info.getValue() || "—",
  }),
  columnHelper.accessor("jobTitle", {
    header: "Role",
    cell: (info) => <LinkedCell value={info.getValue()} url={info.row.original.jobUrl} />,
  }),
  columnHelper.accessor("status", {
    header: "Status",
    cell: (info) => info.getValue() || "—",
  }),
  // Dates arrive as ISO yyyy-mm-dd from DateOnly, so the default string
  // comparison already sorts them correctly and no date parsing is needed.
  // sortUndefined keeps rows with no date at the bottom either way round,
  // rather than letting them lead an ascending sort.
  columnHelper.accessor("postedDate", {
    header: "Posted",
    sortUndefined: "last",
    cell: (info) => info.getValue() || "—",
  }),
  columnHelper.accessor("closeDate", {
    header: "Closes",
    sortUndefined: "last",
    cell: (info) => info.getValue() || "—",
  }),
  columnHelper.display({
    id: "apply",
    header: "Apply",
    enableSorting: false,
    cell: ({ row, table }) => {
      const meta = table.options.meta as JobsTableMeta;

      return (
        <ApplyCell
          state={meta.applyStates[row.original.id]}
          onApply={() => meta.onApply(row.original)}
        />
      );
    },
  }),
];

function JobsTable({
  headingId,
  title,
  jobs,
  search,
  emptyMessage,
  applyStates,
  onApply,
  selectedJobId,
  onSelect,
}: {
  headingId: string;
  title: string;
  jobs: JobDto[];
  search: string;
  emptyMessage?: string;
  applyStates: Record<number, ApplyState>;
  onApply: (job: JobDto) => void;
  selectedJobId: number | null;
  onSelect: (jobId: number) => void;
}) {
  const [sorting, setSorting] = useState<SortingState>([]);

  const meta = useMemo<JobsTableMeta>(() => ({ applyStates, onApply }), [applyStates, onApply]);

  const table = useTable({
    features: jobTableFeatures,
    data: jobs,
    columns: JOB_COLUMNS,
    state: { sorting, globalFilter: search },
    onSortingChange: setSorting,
    getRowId: (job) => String(job.id),
    meta,
  });

  const rows = table.getRowModel().rows;
  const noMatches = jobs.length > 0 && rows.length === 0;

  return (
    <section className="jobs-section" aria-labelledby={headingId}>
      <h2 id={headingId}>
        {title} <span className="count">{rows.length}</span>
      </h2>
      <div className="table-wrap">
        <table>
          <thead>
            {table.getHeaderGroups().map((headerGroup) => (
              <tr key={headerGroup.id}>
                {headerGroup.headers.map((header) => (
                  <SortableHeader
                    key={header.id}
                    label={flexRender(header.column.columnDef.header, header.getContext())}
                    canSort={header.column.getCanSort()}
                    direction={header.column.getIsSorted()}
                    onToggle={header.column.getToggleSortingHandler()}
                  />
                ))}
              </tr>
            ))}
          </thead>
          <tbody>
            {rows.length === 0 ? (
              <tr className="empty-row">
                <td colSpan={JOB_COLUMNS.length}>
                  {noMatches ? `Nothing here matches “${search}”.` : emptyMessage}
                </td>
              </tr>
            ) : (
              rows.map((row) => (
                <tr
                  key={row.id}
                  // Selection follows Apply and follows a click, and stays
                  // put until another row takes it -- so the row being
                  // filled is still obvious after the browser pane has
                  // taken over the screen.
                  className={row.original.id === selectedJobId ? "is-selected" : undefined}
                  aria-selected={row.original.id === selectedJobId}
                  onClick={() => onSelect(row.original.id)}
                >
                  {row.getAllCells().map((cell) => (
                    <td key={cell.id} className={`cell-${cell.column.id}`}>
                      {flexRender(cell.column.columnDef.cell, cell.getContext())}
                    </td>
                  ))}
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    </section>
  );
}

/**
 * A column heading that sorts, per the HIG's rule for tables on the desktop:
 * clicking sorts by that column, and clicking a column that is already
 * sorted reverses it. The arrow appears only on the column doing the
 * sorting, and aria-sort says the same thing to a screen reader.
 *
 * The Apply column is not sortable -- its content is a control, not a value.
 */
function SortableHeader({
  label,
  canSort,
  direction,
  onToggle,
}: {
  label: React.ReactNode;
  canSort: boolean;
  direction: false | "asc" | "desc";
  onToggle: ((event: unknown) => void) | undefined;
}) {
  if (!canSort || !onToggle) {
    return <th scope="col">{label}</th>;
  }

  return (
    <th
      scope="col"
      aria-sort={direction === "asc" ? "ascending" : direction === "desc" ? "descending" : "none"}
    >
      <button type="button" className="column-sort" onClick={onToggle}>
        {label}
        <span className="sort-arrow" aria-hidden="true">
          {direction === "asc" ? "▲" : direction === "desc" ? "▼" : ""}
        </span>
      </button>
    </th>
  );
}

/**
 * Apply opens the posting in the preview pane and fills it, so the label is
 * the action rather than a description of where the fill will land.
 */
function ApplyCell({ state, onApply }: { state?: ApplyState; onApply: () => void }) {
  if (state?.status === "running") {
    return <span className="apply-stack" aria-live="polite">Applying…</span>;
  }

  if (state?.status === "failed") {
    return (
      <span className="apply-stack">
        <span className="apply-error" role="alert">{state.message}</span>
        {state.reference && <code className="apply-reference">{state.reference}</code>}
        <button type="button" onClick={onApply}>Try again</button>
      </span>
    );
  }

  if (state?.status === "done") {
    return (
      <span className="apply-stack">
        <span className="apply-filled">Filled {state.filled.length}</span>
        {state.notFilled.length > 0 && (
          <span className="apply-note">{state.notFilled.length} not found</span>
        )}
        {state.unmatchedLabels.length > 0 && (
          <span className="apply-note" title={state.unmatchedLabels.join(", ")}>
            {state.unmatchedLabels.length} unrecognised
          </span>
        )}
        <button type="button" onClick={onApply}>Apply again</button>
      </span>
    );
  }

  return (
    <span className="apply-stack">
      <button type="button" onClick={onApply} title="Opens the posting in the preview pane and fills it">
        Apply
      </button>
    </span>
  );
}

function LinkedCell({ value, url }: { value?: string; url?: string }) {
  if (!url) {
    return <>{value || "—"}</>;
  }

  // Opens in the native preview pane (MainWindow.xaml.cs) instead of a
  // new browser window -- preventDefault stops the WebView2 default
  // popup behaviour a target="_blank" link would otherwise trigger.
  // href is kept anyway so the link still looks/behaves like a link
  // (status bar preview, right-click "copy link", etc).
  return (
    <a
      href={url}
      onClick={(event) => {
        event.preventDefault();
        window.chrome?.webview?.postMessage({ type: "openJobLink", url });
      }}
    >
      {value || url}
    </a>
  );
}
