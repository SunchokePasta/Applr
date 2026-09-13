interface DbJobDto {
  id: number;
  jobUrl: string;
  jobTitle: string;
  companyName: string;
  companyUrl?: string;
  status?: string;
  postedDate?: string;
  postedDateRaw?: string;
  closeDate?: string;
  closeDateRaw?: string;
  cvRequired: boolean;
  coverLetterRequired: boolean;
  writtenAnswersRequired: boolean;
  visaSponsorship: boolean;
  rawCells?: string[];
  jobIdentityHash: string;
  createdOn: string;
  lastUpdated: string;
}

interface WebViewMessage {
  type: "jobsLoaded" | "jobsFailed";
  jobs?: DbJobDto[];
  message?: string;
}

interface WebViewBridge {
  postMessage(message: { type: "loadJobs" }): void;
  addEventListener(event: "message", listener: (event: MessageEvent<WebViewMessage>) => void): void;
  removeEventListener(event: "message", listener: (event: MessageEvent<WebViewMessage>) => void): void;
}

interface Window {
  chrome?: { webview?: WebViewBridge };
}
