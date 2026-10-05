import { HttpErrorResponse } from '@angular/common/http';
import { Component, ElementRef, NgZone, OnDestroy, OnInit, inject, signal, viewChild } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { ActivatedRoute } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { problemMessage } from '../../core/interceptors';

interface EmbedToken {
  /** Where VeriTrailX (the document processor) runs. */
  baseUrl: string;
  /** Signed by the TETA API for the signed-in person; VeriTrailX trades it for its own session. */
  token: string;
}

/**
 * What VeriTrailX calls things, in TETA's words: TETA already has "projects" (the portfolio), so VeriTrailX's own projects are
 * shown as workspaces here. Other names can be changed too (see docs/embedding/SPEC.md in VeriTrailX).
 */
const LABELS = { project: 'Workspace', project_group: 'Workspace group' };

/** A token fetched less than this long ago is still fresh enough to hand over as it is. */
const FRESH_TOKEN_MS = 60 * 1000;

/** The frame says "ready" within moments; if it never does, it was blocked or the server cannot be reached. */
const FRAME_REPLY_MS = 15 * 1000;

/**
 * Document processing: VeriTrailX shown inside TETA. It has no login of its own here. This page gets a signed token for the
 * signed-in person from the TETA API and passes it to the frame with postMessage; the frame asks again by itself before its
 * session runs out. Route data can open a page other than its start page ({ docprocPath }) or show just that page
 * ({ docprocChrome: 'none' }).
 */
@Component({
  selector: 'teta-docproc',
  standalone: true,
  imports: [MatButtonModule],
  template: `
    <div class="docproc">
      @if (loading() && !error()) { <p class="muted message">Opening document processing…</p> }
      @if (error(); as message) {
        <div class="message error">
          <p>{{ message }}</p>
          <button mat-stroked-button (click)="start()">Try again</button>
        </div>
      }
      @if (frameUrl(); as url) {
        <iframe #frame class="frame" title="Document processing" [src]="url" [class.hidden]="loading()"></iframe>
      }
    </div>
  `,
  styles: [`
    .docproc { position: relative; height: calc(100vh - 96px); min-height: 520px; }
    .frame { display: block; width: 100%; height: 100%; border: 0; background: #fff; }
    .hidden { visibility: hidden; }
    .message { padding: 24px; }
    .error { color: #b71c1c; }
  `]
})
export class DocprocComponent implements OnInit, OnDestroy {
  private readonly api = inject(ApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly zone = inject(NgZone);
  private readonly frame = viewChild<ElementRef<HTMLIFrameElement>>('frame');

  readonly frameUrl = signal<SafeResourceUrl | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  private origin = '';
  private first: { value: EmbedToken; at: number } | null = null;
  private replyTimer: ReturnType<typeof setTimeout> | undefined;
  private readonly listener = (event: MessageEvent) => this.onMessage(event);

  ngOnInit(): void {
    window.addEventListener('message', this.listener);
    this.start();
  }

  ngOnDestroy(): void {
    window.removeEventListener('message', this.listener);
    clearTimeout(this.replyTimer);
  }

  start(): void {
    this.loading.set(true);
    this.error.set(null);
    this.frameUrl.set(null);
    this.api.get<EmbedToken>('docproc/embed-token').subscribe({
      next: value => {
        this.first = { value, at: Date.now() };
        this.origin = new URL(value.baseUrl).origin;
        this.frameUrl.set(this.sanitizer.bypassSecurityTrustResourceUrl(this.frameAddress(this.origin)));
        clearTimeout(this.replyTimer);
        this.replyTimer = setTimeout(() => this.zone.run(() => {
          if (!this.loading()) return;
          this.loading.set(false);
          this.frameUrl.set(null);
          this.error.set('Document processing did not respond. It may not be set up for TETA on the server yet, ' +
            'or the server could not be reached. Please ask an administrator to check the document processing settings.');
        }), FRAME_REPLY_MS);
      },
      error: (error: HttpErrorResponse) => this.fail(error)
    });
  }

  /** The page inside VeriTrailX: from ?path= or the route data (docprocPath), else its start page. */
  private frameAddress(origin: string): string {
    const snapshot = this.route.snapshot;
    const wanted = snapshot.queryParamMap.get('path') || (snapshot.data['docprocPath'] as string | undefined) || '/';
    const path = /^\/(?!\/)/.test(wanted) ? wanted : '/';
    const chrome = snapshot.data['docprocChrome'] === 'none' ? `${path.includes('?') ? '&' : '?'}chrome=none` : '';
    return `${origin}/embed${path === '/' ? '' : path}${chrome}`;
  }

  private onMessage(event: MessageEvent): void {
    const frame = this.frame()?.nativeElement;
    if (!frame || event.source !== frame.contentWindow || event.origin !== this.origin) return;
    const data = event.data;
    if (!data || typeof data.type !== 'string') return;
    switch (data.type) {
      case 'docproc:ready': // also after every reload of the frame
        clearTimeout(this.replyTimer);
        this.sendToken('init');
        break;
      case 'docproc:token-request': // the frame's session is about to run out
        this.sendToken('token');
        break;
      case 'docproc:error':
        this.zone.run(() => {
          this.error.set(String(data.message || 'Document processing could not start.'));
          this.loading.set(false);
        });
        break;
    }
  }

  private sendToken(type: 'init' | 'token'): void {
    const send = (value: EmbedToken) => {
      const target = this.frame()?.nativeElement.contentWindow;
      if (!target) return;
      const message: Record<string, unknown> = { type: `docproc:${type}`, token: value.token };
      if (type === 'init') message['labels'] = LABELS;
      target.postMessage(message, this.origin);
      this.zone.run(() => this.loading.set(false));
    };

    if (this.first && Date.now() - this.first.at < FRESH_TOKEN_MS) {
      const value = this.first.value;
      this.first = null; // each token is handed over once; the next request gets a new one
      send(value);
      return;
    }
    this.api.get<EmbedToken>('docproc/embed-token').subscribe({ next: send, error: (error: HttpErrorResponse) => this.fail(error) });
  }

  private fail(error: HttpErrorResponse): void {
    this.loading.set(false);
    this.frameUrl.set(null);
    this.error.set(problemMessage(error));
  }
}
