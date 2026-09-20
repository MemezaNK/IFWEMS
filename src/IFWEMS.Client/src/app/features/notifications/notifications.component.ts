import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { NotificationsService } from '../../core/notifications/notifications.service';
import { NotificationDto } from '../../core/models/notification.models';

@Component({
  selector: 'app-notifications',
  standalone: true,
  imports: [CommonModule, MatCardModule, MatButtonModule, MatIconModule, MatProgressSpinnerModule],
  templateUrl: './notifications.component.html',
  styleUrl: './notifications.component.scss'
})
export class NotificationsComponent implements OnInit {
  readonly notifications = signal<NotificationDto[]>([]);
  readonly isLoading = signal(true);

  constructor(private readonly notificationsService: NotificationsService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.isLoading.set(true);
    this.notificationsService.getMine().subscribe({
      next: (notifications) => {
        this.notifications.set(notifications);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }

  get unreadCount(): number {
    return this.notifications().filter((n) => !n.isRead).length;
  }

  markRead(id: string): void {
    this.notificationsService.markRead(id).subscribe(() => this.load());
  }

  markAllRead(): void {
    const unread = this.notifications().filter((n) => !n.isRead);
    unread.forEach((n) => this.notificationsService.markRead(n.id).subscribe());
    setTimeout(() => this.load(), 300);
  }
}
