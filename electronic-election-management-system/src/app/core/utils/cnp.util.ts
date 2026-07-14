// Client-side mirror of the backend CnpService (Services/CnpService.cs).
// Used only for instant UI feedback (age/gender/județ preview) while the person types their CNP;
// the backend re-validates and re-derives everything itself, this is never trusted as-is.

export interface CnpInfo {
  birthDate: Date;
  age: number;
  gender: 'M' | 'F';
  countyName: string;
}

const CONTROL_KEY = [2, 7, 9, 1, 4, 6, 3, 5, 8, 2, 7, 9];

const COUNTY_NAMES: Record<string, string> = {
  '01': 'Alba', '02': 'Arad', '03': 'Argeș', '04': 'Bacău',
  '05': 'Bihor', '06': 'Bistrița-Năsăud', '07': 'Botoșani', '08': 'Brașov',
  '09': 'Brăila', '10': 'Buzău', '11': 'Caraș-Severin', '12': 'Cluj',
  '13': 'Constanța', '14': 'Covasna', '15': 'Dâmbovița', '16': 'Dolj',
  '17': 'Galați', '18': 'Gorj', '19': 'Harghita', '20': 'Hunedoara',
  '21': 'Ialomița', '22': 'Iași', '23': 'Ilfov', '24': 'Maramureș',
  '25': 'Mehedinți', '26': 'Mureș', '27': 'Neamț', '28': 'Olt',
  '29': 'Prahova', '30': 'Satu Mare', '31': 'Sălaj', '32': 'Sibiu',
  '33': 'Suceava', '34': 'Teleorman', '35': 'Timiș', '36': 'Tulcea',
  '37': 'Vaslui', '38': 'Vâlcea', '39': 'Vrancea',
  '40': 'București', '41': 'București - Sector 1', '42': 'București - Sector 2',
  '43': 'București - Sector 3', '44': 'București - Sector 4',
  '45': 'București - Sector 5', '46': 'București - Sector 6',
  '51': 'Călărași', '52': 'Giurgiu',
};

const CENTURY_AND_GENDER_BY_S_DIGIT: Record<number, [number, 'M' | 'F']> = {
  1: [1900, 'M'], 2: [1900, 'F'],
  3: [1800, 'M'], 4: [1800, 'F'],
  5: [2000, 'M'], 6: [2000, 'F'],
  7: [1900, 'M'], 8: [1900, 'F'], // resident foreigner
};

export function parseCnp(cnp: string): CnpInfo | null {
  if (!/^\d{13}$/.test(cnp)) return null;

  const d = cnp.split('').map(Number);
  const entry = CENTURY_AND_GENDER_BY_S_DIGIT[d[0]];
  if (!entry) return null;
  const [century, gender] = entry;

  const year = century + d[1] * 10 + d[2];
  const month = d[3] * 10 + d[4];
  const day = d[5] * 10 + d[6];
  const countyCode = `${d[7]}${d[8]}`;

  const countyName = COUNTY_NAMES[countyCode];
  if (!countyName) return null;

  const birthDate = new Date(Date.UTC(year, month - 1, day));
  const isRealCalendarDate =
    birthDate.getUTCFullYear() === year &&
    birthDate.getUTCMonth() === month - 1 &&
    birthDate.getUTCDate() === day;
  if (!isRealCalendarDate || birthDate.getTime() > Date.now()) return null;

  let checksum = 0;
  for (let i = 0; i < 12; i++) checksum += d[i] * CONTROL_KEY[i];
  const remainder = checksum % 11;
  const expectedControlDigit = remainder === 10 ? 1 : remainder;
  if (expectedControlDigit !== d[12]) return null;

  const today = new Date();
  let age = today.getUTCFullYear() - birthDate.getUTCFullYear();
  const hadBirthdayThisYear =
    today.getUTCMonth() > birthDate.getUTCMonth() ||
    (today.getUTCMonth() === birthDate.getUTCMonth() && today.getUTCDate() >= birthDate.getUTCDate());
  if (!hadBirthdayThisYear) age--;

  return { birthDate, age, gender, countyName };
}
