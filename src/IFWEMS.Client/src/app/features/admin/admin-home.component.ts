import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';

interface AdminCard {
  title: string;
  description: string;
  icon: string;
  link: string;
}

@Component({
  selector: 'app-admin-home',
  standalone: true,
  imports: [RouterLink, MatCardModule, MatIconModule],
  templateUrl: './admin-home.component.html',
  styleUrl: './admin-home.component.scss'
})
export class AdminHomeComponent {
  readonly cards: AdminCard[] = [
    { title: 'Users', description: 'Create, view and deactivate user accounts.', icon: 'people', link: '/admin/users' },
    { title: 'Roles', description: 'Manage roles and their permissions.', icon: 'admin_panel_settings', link: '/admin/roles' },
    { title: 'Organisation Units', description: 'Manage the organisational hierarchy.', icon: 'account_tree', link: '/admin/org-units' }
  ];
}
