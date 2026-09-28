import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { BreakpointObserver } from '@angular/cdk/layout';
import { animate, style, transition, trigger } from '@angular/animations';
import { MatBadgeModule } from '@angular/material/badge';
import { MatButtonModule } from '@angular/material/button';
import { MatDividerModule } from '@angular/material/divider';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatMenuModule } from '@angular/material/menu';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Subscription } from 'rxjs';
import { ApiService } from '../core/api.service';
import { AuthService } from '../core/auth.service';
import { NAV, NavGroup } from './nav';

@Component({
  selector: 'teta-shell',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, FormsModule, MatSidenavModule, MatToolbarModule, MatListModule, MatIconModule,
    MatButtonModule, MatMenuModule, MatBadgeModule, MatTooltipModule, MatDividerModule],
  template: `
    <mat-sidenav-container class="shell">
      <mat-sidenav [mode]="mobile() ? 'over' : 'side'" [opened]="!mobile() && navOpen()" (closedStart)="navOpen.set(false)"
                   class="shell-nav no-print">
        <div class="brand">
          <div class="brand-mark">T</div>
          <div class="brand-text">
            <span class="brand-title">TETA IPPCMS</span>
            <span class="brand-subtitle">Portfolio &amp; Contracts</span>
          </div>
        </div>
        <mat-nav-list class="nav-list" dense>
          @for (group of groups(); track group.title) {
            @if (group.title) {
              <button type="button" class="nav-group-header" (click)="toggleGroup(group.title)"
                      [attr.aria-expanded]="isGroupOpen(group.title)">
                <span>{{ group.title }}</span>
                <mat-icon class="chevron" [class.is-open]="isGroupOpen(group.title)">expand_more</mat-icon>
              </button>
              <div class="nav-group-body" [class.is-open]="isGroupOpen(group.title)">
                <div class="nav-group-inner">
                  @for (item of group.items; track item.link) {
                    <a mat-list-item class="nav-item" [routerLink]="item.link" routerLinkActive="active-link"
                       [routerLinkActiveOptions]="{ exact: item.link === '/' }" (click)="mobile() && navOpen.set(false)">
                      <mat-icon matListItemIcon>{{ item.icon }}</mat-icon>
                      <span matListItemTitle>{{ item.label }}</span>
                    </a>
                  }
                </div>
              </div>
            } @else {
              @for (item of group.items; track item.link) {
                <a mat-list-item class="nav-item" [routerLink]="item.link" routerLinkActive="active-link"
                   [routerLinkActiveOptions]="{ exact: item.link === '/' }" (click)="mobile() && navOpen.set(false)">
                  <mat-icon matListItemIcon>{{ item.icon }}</mat-icon>
                  <span matListItemTitle>{{ item.label }}</span>
                </a>
              }
            }
          }
        </mat-nav-list>
      </mat-sidenav>
      <mat-sidenav-content>
        <mat-toolbar class="shell-toolbar no-print">
          <button mat-icon-button (click)="navOpen.set(!navOpen())" aria-label="Toggle navigation"><mat-icon>menu</mat-icon></button>
          <span class="toolbar-title">TETA<span class="toolbar-title-full"><span class="toolbar-title-divider">·</span> Integrated Portfolio, Procurement &amp; Contract Management</span></span>
          <span class="spacer"></span>
          @if (!auth.isSupplier()) {
            <form (ngSubmit)="search()" class="search-box">
              <mat-icon>search</mat-icon>
              <input [(ngModel)]="query" name="q" placeholder="Search projects, procurements, contracts, documents…" aria-label="Global search" />
            </form>
          }
          <button mat-icon-button routerLink="/notifications" matTooltip="Notifications" aria-label="Notifications" class="icon-action">
            <mat-icon [matBadge]="unread() || null" matBadgeColor="warn" matBadgeSize="small">notifications</mat-icon>
          </button>
          <button mat-button [matMenuTriggerFor]="userMenu" class="user-menu-trigger">
            <span class="avatar">{{ initials() }}</span>
            <span class="user-name">{{ auth.user()?.displayName }}</span>
            <mat-icon class="user-caret">arrow_drop_down</mat-icon>
          </button>
          <mat-menu #userMenu="matMenu">
            <div class="menu-user-roles small muted">{{ auth.user()?.roles?.join(', ') }}</div>
            <mat-divider />
            <button mat-menu-item routerLink="/account"><mat-icon>key</mat-icon> Change password</button>
            <a mat-menu-item href="/IFWEMS/"><mat-icon>launch</mat-icon> Open IFWEMS</a>
            <button mat-menu-item (click)="auth.logout()"><mat-icon>logout</mat-icon> Sign out</button>
          </mat-menu>
        </mat-toolbar>
        <div class="route-fade-host" [@routeFade]="currentUrl()">
          <router-outlet />
        </div>
      </mat-sidenav-content>
    </mat-sidenav-container>
  `,
  styles: [`
    :host {
      --nav-bg: #101c34;
      --nav-hover: rgba(255, 255, 255, .07);
      --nav-text: #c7d0e0;
      --nav-text-dim: #8792a8;
      --nav-border: rgba(255, 255, 255, .08);
      --nav-active-bg: rgba(59, 130, 246, .22);
      --nav-accent: #60a5fa;
    }
    .shell { height: 100vh; }

    /* ---------- Sidenav: dark navy chrome, light content canvas ---------- */
    .shell-nav {
      width: 268px;
      background: var(--nav-bg);
      border-right: 1px solid var(--nav-border);
      display: flex;
      flex-direction: column;
    }
    .brand {
      display: flex;
      align-items: center;
      gap: 10px;
      padding: 16px;
      border-bottom: 1px solid var(--nav-border);
    }
    .brand-mark {
      width: 32px;
      height: 32px;
      border-radius: 8px;
      background: linear-gradient(135deg, #2563eb, #60a5fa);
      color: #fff;
      display: flex;
      align-items: center;
      justify-content: center;
      font-weight: 600;
      font-size: 15px;
      flex: 0 0 auto;
    }
    .brand-text { display: flex; flex-direction: column; line-height: 1.2; min-width: 0; }
    .brand-title { font-weight: 600; font-size: 14px; letter-spacing: .01em; color: #fff; }
    .brand-subtitle { font-size: 11px; color: var(--nav-text-dim); }

    .nav-list {
      padding-top: 4px;
      overflow-y: auto;
      /* Verified live: with only overflow-y set, a vertical scrollbar appearing when a group
         expands nudges scrollWidth just past clientWidth (a well-known cross-browser quirk), and
         because the CSS spec computes overflow-x as auto too whenever overflow-y isn't visible,
         that shows up as a spurious horizontal scrollbar. Pin it off explicitly. */
      overflow-x: hidden;
      flex: 1 1 auto;
      /* Repoint Material's MDC list colour tokens at our dark palette (belt); the plain color
         overrides below on .nav-item/.mat-icon cover the ligature-icon font either way (suspenders). */
      --mdc-list-list-item-label-text-color: var(--nav-text);
      --mdc-list-list-item-leading-icon-icon-color: var(--nav-text-dim);
      --mdc-list-list-item-hover-label-text-color: #fff;
      --mdc-list-list-item-hover-leading-icon-icon-color: #fff;
      --mdc-list-list-item-hover-state-layer-color: #fff;
      --mdc-list-list-item-focus-state-layer-color: #fff;
      --mdc-list-list-item-hover-state-layer-opacity: .06;
    }
    /* Sidenav scrollbar thumb needs to be light to show up against the dark navy background --
       the global (dark-on-light) default from styles.scss would be nearly invisible here. */
    .shell-nav, .nav-list {
      scrollbar-color: rgba(255, 255, 255, .25) transparent;
    }
    .shell-nav::-webkit-scrollbar-thumb, .nav-list::-webkit-scrollbar-thumb {
      background-color: rgba(255, 255, 255, .25);
    }
    .shell-nav::-webkit-scrollbar-thumb:hover, .nav-list::-webkit-scrollbar-thumb:hover {
      background-color: rgba(255, 255, 255, .4);
    }
    .nav-group-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      width: 100%;
      padding: 10px 16px 6px;
      margin: 0;
      border: 0;
      background: transparent;
      color: var(--nav-text-dim);
      font-size: 11px;
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: .06em;
      cursor: pointer;
      font-family: inherit;
      transition: color .15s ease;
    }
    .nav-group-header:hover { color: #fff; }
    .nav-group-header .chevron {
      font-size: 18px;
      width: 18px;
      height: 18px;
      transition: transform .18s ease;
    }
    .nav-group-header .chevron.is-open { transform: rotate(180deg); }

    .nav-group-body {
      display: grid;
      grid-template-rows: 0fr;
      transition: grid-template-rows .2s ease;
    }
    .nav-group-body.is-open { grid-template-rows: 1fr; }
    .nav-group-inner { overflow: hidden; min-height: 0; }

    .nav-item,
    .nav-item .mdc-list-item__primary-text,
    .nav-item .mat-icon {
      color: var(--nav-text) !important;
      transition: background-color .12s ease, color .12s ease;
    }
    .nav-item {
      border-radius: 0 20px 20px 0;
      margin-right: 8px;
    }
    .nav-item:hover,
    .nav-item:hover .mdc-list-item__primary-text,
    .nav-item:hover .mat-icon {
      background: var(--nav-hover);
      color: #fff !important;
    }
    .active-link,
    .active-link .mdc-list-item__primary-text {
      background: var(--nav-active-bg) !important;
      font-weight: 600;
      color: #fff !important;
      box-shadow: inset 3px 0 0 var(--nav-accent);
    }
    .active-link .mat-icon { color: var(--nav-accent) !important; }

    /* ---------- Toolbar: same dark navy as the sidenav for one unified frame ---------- */
    .shell-toolbar {
      position: sticky;
      top: 0;
      z-index: 10;
      background: var(--nav-bg);
      color: #fff;
      box-shadow: 0 1px 3px rgba(0, 0, 0, .18);
      gap: 4px;
    }
    .toolbar-title { margin-left: 8px; font-size: 15px; font-weight: 500; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .toolbar-title-full { white-space: nowrap; }
    .toolbar-title-divider { opacity: .6; margin: 0 2px; }
    .spacer { flex: 1 1 auto; }

    /* Below ~1024px the full descriptive title competes with the search box; drop the suffix
       first since the brand mark + "TETA" already identify the app. */
    @media (max-width: 1024px) {
      .toolbar-title-full { display: none; }
    }
    /* Below ~880px there isn't room for the search box next to notifications/user menu without
       them colliding -- hide it and rely on the sidenav for navigation (global search is still
       reachable once the layout has room). */
    @media (max-width: 880px) {
      .search-box { display: none; }
    }

    .search-box {
      display: flex;
      align-items: center;
      gap: 6px;
      background: rgba(255, 255, 255, .1);
      border: 1px solid rgba(255, 255, 255, .12);
      border-radius: 20px;
      padding: 0 12px;
      margin-right: 6px;
      transition: background-color .15s ease, box-shadow .15s ease;
    }
    .search-box:focus-within { background: rgba(255, 255, 255, .16); box-shadow: 0 0 0 2px rgba(96, 165, 250, .45); }
    .search-box mat-icon { opacity: .85; font-size: 20px; width: 20px; height: 20px; }
    .search-box input {
      background: transparent;
      border: 0;
      color: #fff;
      outline: none;
      width: 300px;
      max-width: 32vw;
      padding: 9px 0;
      font-size: 13.5px;
    }
    .search-box input::placeholder { color: rgba(255, 255, 255, .7); }

    .icon-action { transition: transform .12s ease; color: #fff; }
    .icon-action:hover { transform: translateY(-1px); }

    /* Verified live: Material's plain (non-icon) mat-button applies its own near-black
       "unthemed" text colour directly on the button and its label span, which otherwise beats
       the white colour this element would just inherit from the dark toolbar -- the icon-only
       toolbar buttons (menu toggle, notifications) don't have this problem, only this one does. */
    .user-menu-trigger, .user-menu-trigger .mdc-button__label, .user-menu-trigger .mat-icon,
    .user-menu-trigger .user-name, .user-menu-trigger .user-caret {
      color: #fff !important;
    }
    .user-menu-trigger { display: flex; align-items: center; gap: 8px; flex: 0 0 auto; }
    .avatar {
      width: 26px;
      height: 26px;
      border-radius: 50%;
      background: var(--nav-accent);
      color: #0b162b;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      font-size: 11px;
      font-weight: 700;
      letter-spacing: .02em;
      flex: 0 0 auto;
    }
    .user-name { max-width: 160px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    @media (max-width: 700px) {
      .user-name { display: none; }
    }
    .user-caret { opacity: .8; }
    .menu-user-roles { padding: 8px 16px; }

    /* ---------- Route transition ---------- */
    .route-fade-host { min-height: calc(100vh - 64px); }
  `],
  animations: [
    trigger('routeFade', [
      transition('* <=> *', [
        style({ opacity: 0 }),
        animate('160ms ease-out', style({ opacity: 1 }))
      ])
    ])
  ]
})
export class ShellComponent implements OnInit, OnDestroy {
  readonly auth = inject(AuthService);
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  private readonly breakpoints = inject(BreakpointObserver);
  private poll?: ReturnType<typeof setInterval>;
  private routerSub?: Subscription;

  readonly navOpen = signal(true);
  readonly mobile = signal(false);
  readonly unread = signal(0);
  readonly currentUrl = signal('');
  readonly openGroups = signal<Set<string>>(new Set());
  query = '';

  readonly groups = computed<NavGroup[]>(() => {
    const user = this.auth.user();
    const supplier = this.auth.isSupplier();
    return NAV.map(g => ({
      title: g.title,
      items: g.items.filter(i => (supplier ? i.link === '/' || i.link.startsWith('/portal') : true) && (!i.permissions || this.auth.hasAny(...i.permissions)))
    })).filter(g => g.items.length > 0 && !!user);
  });

  readonly initials = computed(() => {
    const name = this.auth.user()?.displayName ?? '';
    const parts = name.trim().split(/\s+/).filter(Boolean);
    if (parts.length === 0) return '?';
    return (parts[0][0] + (parts.length > 1 ? parts[parts.length - 1][0] : '')).toUpperCase();
  });

  ngOnInit(): void {
    this.breakpoints.observe('(max-width: 960px)').subscribe(r => this.mobile.set(r.matches));
    this.refreshUnread();
    this.poll = setInterval(() => this.refreshUnread(), 60_000);

    this.currentUrl.set(this.router.url);
    this.syncOpenGroupWithRoute();
    this.routerSub = this.router.events.subscribe(event => {
      if (event instanceof NavigationEnd) {
        this.currentUrl.set(event.urlAfterRedirects);
        this.syncOpenGroupWithRoute();
      }
    });
  }

  ngOnDestroy(): void {
    if (this.poll) clearInterval(this.poll);
    this.routerSub?.unsubscribe();
  }

  refreshUnread(): void {
    this.api.get<{ count: number }>('notifications/unread-count').subscribe({ next: r => this.unread.set(r.count), error: () => undefined });
  }

  search(): void {
    if (this.query.trim().length >= 2) this.router.navigate(['/search'], { queryParams: { q: this.query.trim() } });
  }

  toggleGroup(title: string): void {
    const next = new Set(this.openGroups());
    if (next.has(title)) next.delete(title); else next.add(title);
    this.openGroups.set(next);
  }

  isGroupOpen(title: string): boolean {
    return this.openGroups().has(title);
  }

  /** Keeps the sidebar tidy: whichever group contains the active route is expanded automatically. */
  private syncOpenGroupWithRoute(): void {
    const url = this.currentUrl().split('?')[0];
    const match = this.groups().find(g => g.title && g.items.some(i => i.link !== '/' && url.startsWith(i.link)));
    if (match?.title) {
      const next = new Set(this.openGroups());
      next.add(match.title);
      this.openGroups.set(next);
    }
  }
}
