import { test, expect } from '@playwright/test';

test.describe('Organisation Units (FR-004)', () => {
  test('admin can log in and view org units hierarchy', async ({ page }) => {
    await page.goto('/login');

    await page.fill('#username', 'admin');
    await page.fill('#password', 'Admin@12345');
    await page.click('button[type="submit"]');

    // Should navigate away from the login page after a successful sign-in.
    await expect(page).not.toHaveURL(/\/login$/, { timeout: 10000 });

    await page.goto('/admin/org-units');
    await expect(page.locator('h1')).toHaveText('Organisation Units');

    // The seeded department-level unit should be visible in the table.
    await expect(page.locator('table.org-units-table')).toContainText('Department of Health');
  });

  test('admin can create a new organisation unit', async ({ page }) => {
    await page.goto('/login');
    await page.fill('#username', 'admin');
    await page.fill('#password', 'Admin@12345');
    await page.click('button[type="submit"]');
    await expect(page).not.toHaveURL(/\/login$/, { timeout: 10000 });

    await page.goto('/admin/org-units');
    await expect(page.locator('table.org-units-table')).toBeVisible();

    const uniqueCode = `PW-TEST-${Date.now()}`;
    await page.fill('input[name="code"]', uniqueCode);
    await page.fill('input[name="name"]', 'Playwright Test Facility');
    await page.fill('input[name="level"]', 'Facility');

    await page.click('button[type="submit"]:has-text("Create Unit")');

    await expect(page.locator('table.org-units-table')).toContainText(uniqueCode, { timeout: 10000 });
  });
});
