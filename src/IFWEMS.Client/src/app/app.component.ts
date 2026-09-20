import { Component, computed, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatMenuModule } from '@angular/material/menu';
import { MatTooltipModule } from '@angular/material/tooltip';
import { CommonModule } from '@angular/common';
import { AuthService } from './core/auth/auth.service';
import { GlobalSearchComponent } from './core/shared/global-search.component';

interface NavLink {
  path?: string;
  label: string;
  icon: string;
  roles?: string[];
  children?: NavLink[];
}

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    CommonModule,
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatToolbarModule,
    MatSidenavModule,
    MatListModule,
    MatIconModule,
    MatButtonModule,
    MatMenuModule,
    MatTooltipModule,
    GlobalSearchComponent
  ],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent {
  title = 'IFWEMS';
  isDarkTheme = false;
  expandedItems = signal<Record<string, boolean>>({});
  navCollapsed = signal(false);
  currentUrl = signal('');

  readonly navLinks: NavLink[] = [
    { path: '/dashboard', label: 'Dashboard', icon: 'dashboard' },
    { path: '/cases', label: 'Case Management', icon: 'folder_open' },
    { 
      label: 'Compliance', 
      icon: 'fact_check',
      children: [
        { path: '/compliance/check', label: 'Compliance Check', icon: 'check_circle' },
        { path: '/compliance/override', label: 'Emergency Override', icon: 'warning', roles: ['ApprovingOfficial', 'SystemAdministrator'] },
        { path: '/compliance/transactions', label: 'Transaction Register', icon: 'receipt_long', roles: ['ComplianceOfficer', 'ApprovingOfficial', 'ReadOnlyAuditor', 'SystemAdministrator'] }
      ]
    },
    { 
      label: 'Contracts & Suppliers', 
      icon: 'business',
      children: [
        { path: '/contracts', label: 'Contracts', icon: 'description', roles: ['ContractOfficer', 'SystemAdministrator'] },
        { path: '/suppliers', label: 'Suppliers', icon: 'business', roles: ['ContractOfficer', 'SystemAdministrator'] }
      ]
    },
    { path: '/notifications', label: 'Notifications', icon: 'notifications' },
    { path: '/reports', label: 'Reports', icon: 'assessment' },
    { path: '/audit', label: 'Audit Trail', icon: 'history', roles: ['ReadOnlyAuditor', 'ComplianceOfficer', 'SystemAdministrator'] },
    { 
      label: 'Administration', 
      icon: 'admin_panel_settings',
      roles: ['SystemAdministrator'],
      children: [
        { path: '/admin/users', label: 'Users', icon: 'people' },
        { path: '/admin/roles', label: 'Roles', icon: 'security' },
        { path: '/admin/org-units', label: 'Organisational Units', icon: 'apartment' },
        { path: '/admin/rules', label: 'Compliance Rules', icon: 'rule' },
        { path: '/admin/sla', label: 'SLA Policies', icon: 'schedule' },
        { path: '/admin/notifications', label: 'Notification Templates', icon: 'email' },
        { path: '/admin/system-config', label: 'System Configuration', icon: 'settings' }
      ]
    }
  ];

  readonly currentUser;
  readonly isAuthenticated;

  constructor(private readonly authService: AuthService, private readonly router: Router) {
    this.currentUser = this.authService.currentUser;
    this.isAuthenticated = computed(() => this.currentUser() !== null);
    this.currentUrl.set(this.router.url);
    this.router.events.subscribe((event) => {
      if (event instanceof NavigationEnd) {
        this.currentUrl.set(event.urlAfterRedirects);
      }
    });
  }

  visibleLinks(): NavLink[] {
    return this.navLinks.filter((link) => this.isLinkVisible(link));
  }

  private isLinkVisible(link: NavLink): boolean {
    if (link.roles && !this.authService.hasRole(...link.roles)) {
      return false;
    }
    if (link.children) {
      return link.children.some((child) => this.isLinkVisible(child));
    }
    return true;
  }

  visibleChildren(parent: NavLink): NavLink[] {
    return parent.children?.filter((child) => this.isLinkVisible(child)) ?? [];
  }

  toggleExpanded(link: NavLink): void {
    if (this.navCollapsed()) {
      // Coming from icon-only mode: expand the rail and open this group directly.
      this.navCollapsed.set(false);
      this.expandedItems.set({ [link.label]: true });
      return;
    }
    const current = this.expandedItems();
    const isOpen = current[link.label] ?? this.isGroupActive(link);
    this.expandedItems.set({ ...current, [link.label]: !isOpen });
  }

  isExpanded(link: NavLink): boolean {
    const explicit = this.expandedItems()[link.label];
    return explicit ?? this.isGroupActive(link);
  }

  isGroupActive(link: NavLink): boolean {
    if (!link.children) {
      return false;
    }
    const url = this.currentUrl();
    return link.children.some((child) => !!child.path && url.startsWith(child.path));
  }

  toggleNavCollapsed(): void {
    this.navCollapsed.update((collapsed) => !collapsed);
    if (this.navCollapsed()) {
      this.expandedItems.set({});
    }
  }

  toggleTheme(): void {
    this.isDarkTheme = !this.isDarkTheme;
    document.body.classList.toggle('dark-theme', this.isDarkTheme);
  }

  logout(): void {
    this.authService.logout();
    window.location.href = '/login';
  }
}
