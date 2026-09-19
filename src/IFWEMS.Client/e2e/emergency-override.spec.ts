import { test, expect } from '@playwright/test';

test.describe('Emergency Override - Org Unit / Supplier lookups', () => {
  test('org unit and supplier fields render as populated dropdowns', async ({ page }) => {
    await page.goto('/login');
    await page.fill('#username', 'admin');
    await page.fill('#password', 'Admin@12345');
    await page.click('button[type="submit"]');
    await expect(page).not.toHaveURL(/\/login$/, { timeout: 10000 });

    await page.goto('/compliance/override');

    const orgUnitSelect = page.locator('select#orgUnitId');
    await expect(orgUnitSelect).toBeVisible();
    await expect
      .poll(async () => orgUnitSelect.locator('option').count(), { timeout: 10000 })
      .toBeGreaterThan(1);
    await orgUnitSelect.selectOption({ index: 1 });

    const supplierSelect = page.locator('select#supplierId');
    await expect(supplierSelect).toBeVisible();
    const supplierOptionCount = await supplierSelect.locator('option').count();
    expect(supplierOptionCount).toBeGreaterThanOrEqual(1);

    const documentSelect = page.locator('select#evidenceDocumentId');
    await expect(documentSelect).toBeVisible();
    const documentOptionCount = await documentSelect.locator('option').count();
    expect(documentOptionCount).toBeGreaterThanOrEqual(1);
  });
});
