import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { BreakpointObserver } from '@angular/cdk/layout';
import { MatBadgeModule } from '@angular/material/badge';
import { MatButtonModule } from '@angular/material/button';
import { MatDividerModule } from '@angular/material/divider';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatMenuModule } from '@angular/material/menu';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ApiService } from '../core/api.service';
import { AuthService } from '../core/auth.service';
import { NAV, NavGroup } from './nav';

@Component({
  selector: 'teta-shell',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, FormsModule, MatSidenavModule, MatToolbarModule, MatListModule, MatIconModule,
    MatButtonModule, MatMenuModule, MatBadgeModule, MatTooltipModule, MatDividerModule],
  template: `
    <mat-sidenav-container style="height: 100vh">
      <mat-sidenav [mode]="mobile() ? 'over' : 'side'" [opened]="!mobile() && navOpen()" (closedStart)="navOpen.set(false)" style="width: 272px" class="no-print">
        <div style="padding: 16px 16px 8px; font-weight: 500">TETA IPPCMS</div>
        <mat-nav-list dense>
          @for (group of groups(); track group.title) {
            @if (group.title) { <div class="muted small" style="padding: 12px 16px 4px; text-transform: uppercase; letter-spacing: .04em">{{ group.title }}</div> }
            @for (item of group.items; track item.link) {
              <a mat-list-item [routerLink]="item.link" routerLinkActive="active-link" [routerLinkActiveOptions]="{ exact: item.link === '/' }"
                 (click)="mobile() && navOpen.set(false)">
                <mat-icon matListItemIcon>{{ item.icon }}</mat-icon>
                <span matListItemTitle>{{ item.label }}</span>
              </a>
            }
          }
        </mat-nav-list>
      </mat-sidenav>
      <mat-sidenav-content>
        <mat-toolbar color="primary" class="no-print" style="position: sticky; top: 0; z-index: 10">
          <button mat-icon-button (click)="navOpen.set(!navOpen())" aria-label="Toggle navigation"><mat-icon>menu</mat-icon></button>
          <span style="margin-left: 8px">TETA · Integrated Portfolio, Procurement &amp; Contract Management</span>
          <span class="spacer"></span>
          @if (!auth.isSupplier()) {
            <form (ngSubmit)="search()" style="display: flex; align-items: center; background: rgba(255,255,255,.15); border-radius: 6px; padding: 0 8px; margin-right: 8px">
              <mat-icon>search</mat-icon>
              <input [(ngModel)]="query" name="q" placeholder="Search projects, procurements, contracts, documents…" aria-label="Global search"
                     style="background: transparent; border: 0; color: white; outline: none; width: 320px; padding: 8px" />
            </form>
          }
          <button mat-icon-button routerLink="/notifications" matTooltip="Notifications" aria-label="Notifications">
            <mat-icon [matBadge]="unread() || null" matBadgeColor="warn" matBadgeSize="small">notifications</mat-icon>
          </button>
          <button mat-button [matMenuTriggerFor]="userMenu">
            <mat-icon>account_circle</mat-icon> {{ auth.user()?.displayName }}
          </button>
          <mat-menu #userMenu="matMenu">
            <div style="padding: 8px 16px" class="small muted">{{ auth.user()?.roles?.join(', ') }}</div>
            <mat-divider />
            <button mat-menu-item routerLink="/account"><mat-icon>key</mat-icon> Change password</button>
            <a mat-menu-item href="/IFWEMS/"><mat-icon>launch</mat-icon> Open IFWEMS</a>
            <button mat-menu-item (click)="auth.logout()"><mat-icon>logout</mat-icon> Sign out</button>
          </mat-menu>
        </mat-toolbar>
        <router-outlet />
      </mat-sidenav-content>
    </mat-sidenav-container>
  `,
  styles: [`.active-link { background: rgba(37, 99, 235, .1); font-weight: 500; }`]
})
export class ShellComponent implements OnInit, OnDestroy {
  readonly auth = inject(AuthService);
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  private readonly breakpoints = inject(BreakpointObserver);
  private poll?: ReturnType<typeof setInterval>;

  readonly navOpen = signal(true);
  readonly mobile = signal(false);
  readonly unread = signal(0);
  query = '';

  readonly groups = computed<NavGroup[]>(() => {
    const user = this.auth.user();
    const supplier = this.auth.isSupplier();
    return NAV.map(g => ({
      title: g.title,
      items: g.items.filter(i => (supplier ? i.link === '/' || i.link.startsWith('/portal') : true) && (!i.permissions || this.auth.hasAny(...i.permissions)))
    })).filter(g => g.items.length > 0 && !!user);
  });

  ngOnInit(): void {
    this.breakpoints.observe('(max-width: 960px)').subscribe(r => this.mobile.set(r.matches));
    this.refreshUnread();
    this.poll = setInterval(() => this.refreshUnread(), 60_000);
  }

  ngOnDestroy(): void {
    if (this.poll) clearInterval(this.poll);
  }

  refreshUnread(): void {
    this.api.get<{ count: number }>('notifications/unread-count').subscribe({ next: r => this.unread.set(r.count), error: () => undefined });
  }

  search(): void {
    if (this.query.trim().length >= 2) this.router.navigate(['/search'], { queryParams: { q: this.query.trim() } });
  }
}
