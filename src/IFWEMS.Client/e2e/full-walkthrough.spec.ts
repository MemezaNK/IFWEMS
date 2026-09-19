import { test, expect } from '@playwright/test';

test.describe('Full user walkthrough (SRS validation)', () => {
  test('login, dashboard, cases, compliance check, override, admin pages all load', async ({ page }) => {
    // Landing page
    await page.goto('/');
    await expect(page.locator('body')).toBeVisible();

    // Login
    await page.goto('/login');
    await page.fill('#username', 'admin');
    await page.fill('#password', 'Admin@12345');
    await page.click('button[type="submit"]');
    await expect(page).not.toHaveURL(/\/login$/, { timeout: 10000 });

    // Dashboard
    await page.goto('/dashboard');
    await expect(page.locator('h1')).toContainText('Dashboard');
    const cardLinks = await page.locator('a[href]').evaluateAll((els) => els.map((e) => e.getAttribute('href')));
    console.log('Dashboard links:', cardLinks);

    // Cases
    await page.goto('/cases');
    await expect(page.locator('h1')).toHaveText('Case Management');

    // Compliance check
    await page.goto('/compliance/check');
    await expect(page.locator('h1')).toContainText('Compliance Check');

    // Emergency override
    await page.goto('/compliance/override');
    await expect(page.locator('h1')).toContainText('Emergency');

    // Admin home
    await page.goto('/admin');
    await expect(page.locator('body')).toBeVisible();
    const adminLinks = await page.locator('a[href]').evaluateAll((els) => els.map((e) => e.getAttribute('href')));
    console.log('Admin links:', adminLinks);

    // Admin sub-pages
    await page.goto('/admin/users');
    await expect(page.locator('body')).toBeVisible();

    await page.goto('/admin/roles');
    await expect(page.locator('body')).toBeVisible();

    await page.goto('/admin/org-units');
    await expect(page.locator('h1')).toHaveText('Organisation Units');

    // Non-existent modules per SRS - confirm they are NOT routed (documents current gap)
    await page.goto('/investigations');
    console.log('URL after /investigations nav:', page.url());

    await page.goto('/recoveries');
    console.log('URL after /recoveries nav:', page.url());

    await page.goto('/documents');
    console.log('URL after /documents nav:', page.url());

    await page.goto('/contracts');
    console.log('URL after /contracts nav:', page.url());

    await page.goto('/suppliers');
    console.log('URL after /suppliers nav:', page.url());

    await page.goto('/reports');
    console.log('URL after /reports nav:', page.url());
  });
});
