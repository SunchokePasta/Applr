interface JobDto {
  id: number;
  rawJobId: number;
  companyId: number;
  jobTitle: string;
  jobUrl: string;
  companyName: string;
  status: string;
  postedDate?: string;
  closeDate?: string;
  createdOn: string;
  lastUpdated: string;
}

interface WebViewMessage {
  type: "jobsLoaded" | "jobsFailed" | "applyFinished" | "applyFailed";
  existingJobs?: JobDto[];
  newJobs?: JobDto[];
  /** Already safe to display: phrased for a user by whichever tier failed. */
  message?: string;
  /**
   * Ties what's on screen to a specific line in a log file. The same
   * value appears in Applr.RestApi's log, Applr.API's log and the
   * desktop log, depending on how far the request got -- so quoting it
   * is enough to find the stack trace.
   */
  reference?: string;
  /** Which row an apply reply belongs to. Absent on the jobs* messages. */
  jobId?: number;
  /** Profile keys ApplrFiller typed into the form. */
  filled?: string[];
  /** Profile keys it had a value for but found no field for. */
  notFilled?: string[];
  /**
   * Labels on the page that no stored pattern claimed. Worth showing: a
   * form needing new patterns is otherwise a silent half-fill.
   */
  unmatchedLabels?: string[];
}

interface WebViewBridge {
  postMessage(
    message:
      | { type: "loadJobs" }
      | { type: "openJobLink"; url: string }
      | { type: "applyToJob"; jobId: number; url?: string }
  ): void;
  addEventListener(event: "message", listener: (event: MessageEvent<WebViewMessage>) => void): void;
  removeEventListener(event: "message", listener: (event: MessageEvent<WebViewMessage>) => void): void;
}

interface Window {
  chrome?: { webview?: WebViewBridge };
}
