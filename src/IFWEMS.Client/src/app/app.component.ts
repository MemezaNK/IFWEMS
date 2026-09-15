import { Component, computed } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatMenuModule } from '@angular/material/menu';
import { AuthService } from './core/auth/auth.service';

interface NavLink {
  path: string;
  label: string;
  icon: string;
  roles?: string[];
}

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
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

  readonly navLinks: NavLink[] = [
    { path: '/dashboard', label: 'Dashboard', icon: 'dashboard' },
    { path: '/cases', label: 'Cases', icon: 'folder_open' },
    { path: '/compliance/check', label: 'Compliance Check', icon: 'fact_check' },
    { path: '/compliance/override', label: 'Emergency Override', icon: 'warning', roles: ['ApprovingOfficial', 'SystemAdministrator'] }
  ];

  readonly currentUser;
  readonly isAuthenticated;

  constructor(private readonly authService: AuthService) {
    this.currentUser = this.authService.currentUser;
    this.isAuthenticated = computed(() => this.currentUser() !== null);
  }

  visibleLinks(): NavLink[] {
    return this.navLinks.filter((link) => !link.roles || this.authService.hasRole(...link.roles));
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
