import { Component, computed, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatMenuModule } from '@angular/material/menu';
import { CommonModule } from '@angular/common';
import { AuthService } from './core/auth/auth.service';

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
    MatMenuModule
  ],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent {
  title = 'IFWEMS';
  isDarkTheme = false;
  expandedItems = signal<Record<string, boolean>>({});

  readonly navLinks: NavLink[] = [
    { path: '/dashboard', label: 'Dashboard', icon: 'dashboard' },
    { path: '/cases', label: 'Case Management', icon: 'folder_open' },
    { 
      label: 'Compliance', 
      icon: 'fact_check',
      children: [
        { path: '/compliance/check', label: 'Compliance Check', icon: 'check_circle' },
        { path: '/compliance/override', label: 'Emergency Override', icon: 'warning', roles: ['ApprovingOfficial', 'SystemAdministrator'] }
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
    { 
      label: 'Administration', 
      icon: 'admin_panel_settings',
      roles: ['SystemAdministrator'],
      children: [
        { path: '/admin/users', label: 'Users', icon: 'people' },
        { path: '/admin/roles', label: 'Roles', icon: 'security' },
        { path: '/admin/org-units', label: 'Organisational Units', icon: 'apartment' },
        { path: '/admin/rules', label: 'Compliance Rules', icon: 'rule' },
        { path: '/admin/notifications', label: 'Notification Templates', icon: 'email' },
        { path: '/admin/system-config', label: 'System Configuration', icon: 'settings' }
      ]
    }
  ];

  readonly currentUser;
  readonly isAuthenticated;

  constructor(private readonly authService: AuthService) {
    this.currentUser = this.authService.currentUser;
    this.isAuthenticated = computed(() => this.currentUser() !== null);
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

  toggleExpanded(itemLabel: string): void {
    const current = this.expandedItems();
    this.expandedItems.set({
      ...current,
      [itemLabel]: !current[itemLabel]
    });
  }

  isExpanded(itemLabel: string): boolean {
    return this.expandedItems()[itemLabel] ?? false;
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
