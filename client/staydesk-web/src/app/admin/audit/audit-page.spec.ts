import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideNativeDateAdapter } from '@angular/material/core';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTranslateService } from '@ngx-translate/core';
import { Paged } from '../../public/catalog.models';
import { AuditEntry } from '../../shared/audit.models';
import { AuditPage } from './audit-page';

const staffUser = '01a1e526-0000-7000-8000-000000000001';
const roomId = '01a1e526-0000-7000-8000-000000000002';

const deactivated: AuditEntry = {
  id: 'a1',
  timestamp: '2026-11-18T09:00:00Z',
  userId: staffUser,
  userDisplayName: 'Jonas Becker',
  entityName: 'Room',
  entityId: roomId,
  action: 'Deactivated',
  changes: { IsActive: { old: true, new: false } },
  correlationId: 'c1',
};

function page(items: AuditEntry[]): Paged<AuditEntry> {
  return { items, page: 1, pageSize: 50, totalCount: items.length, totalPages: 1 };
}

// AUD-3: entity, user and date filters in the URL; one entry's changes open on demand.
describe('AuditPage', () => {
  let http: HttpTestingController;
  let harness: RouterTestingHarness;

  async function open(url: string): Promise<AuditPage> {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'admin/audit', component: AuditPage, providers: [provideNativeDateAdapter()] },
        ]),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTranslateService(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create();
    return harness.navigateByUrl(url, AuditPage);
  }

  function listRequest() {
    return http.expectOne((request) => request.url === '/api/v1/audit');
  }

  afterEach(() => http.verify());

  it('sends the filters in the URL and drops ones that are not valid', async () => {
    await open(
      `/admin/audit?entity=Room&entityId=${roomId}&userId=nobody&from=2026-11-01&to=2026-11-18`,
    );

    const params = listRequest().request.params;
    expect(params.get('entity')).toBe('Room');
    expect(params.get('entityId')).toBe(roomId);
    expect(params.has('userId')).toBe(false);
    expect(params.get('from')).toBe('2026-11-01');
    expect(params.get('to')).toBe('2026-11-18');
  });

  it('filters by the user whose name was clicked', async () => {
    await open('/admin/audit');
    listRequest().flush(page([deactivated]));
    await harness.fixture.whenStable();

    const userButton = [...harness.fixture.nativeElement.querySelectorAll('td button')].find(
      (b: HTMLButtonElement) => b.textContent!.includes('Jonas Becker'),
    ) as HTMLButtonElement;
    userButton.click();
    await harness.fixture.whenStable();

    expect(TestBed.inject(Router).url).toBe(`/admin/audit?userId=${staffUser}`);
    expect(listRequest().request.params.get('userId')).toBe(staffUser);
  });

  it('shows an entry’s old and new values only when opened', async () => {
    const component = await open('/admin/audit');
    listRequest().flush(page([deactivated]));
    await harness.fixture.whenStable();
    expect(harness.fixture.nativeElement.querySelector('sd-audit-changes')).toBeNull();

    component.toggle(deactivated);
    await harness.fixture.whenStable();

    expect(harness.fixture.nativeElement.querySelector('sd-audit-changes')?.textContent).toMatch(
      /IsActive\s*true\s*false/,
    );
  });
});
