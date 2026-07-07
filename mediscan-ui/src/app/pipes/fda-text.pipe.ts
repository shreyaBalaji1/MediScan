import { Pipe, PipeTransform } from '@angular/core';

// openFDA label text repeats the section name as the first word of the value
// itself (e.g. "Warnings Reye's syndrome : ...", "Purpose Pain reliever").
// Strip that redundant lead-in before displaying it.
function stripLabelPrefix(text: string): string {
  return text.replace(/^(Warnings|Purpose|Drug Interactions)\s+/i, '').trim();
}

@Pipe({ name: 'stripFdaPrefix', standalone: true })
export class StripFdaPrefixPipe implements PipeTransform {
  transform(text: string | null | undefined): string {
    if (!text) return '';
    return stripLabelPrefix(text);
  }
}

// The FDA's OTC "Drug Facts" panel format (21 CFR 201.66) uses a fixed set of
// standard subheadings across every product — "Do not use", "Ask a doctor
// before use if", "Stop use and ask a doctor if", etc. openFDA's plain-text
// export runs them into the surrounding text with no punctuation, so they're
// used here as extra, drug-agnostic split points.
const STANDARD_HEADINGS =
  'Do not use|Ask a doctor(?: or pharmacist)? before use|When using this product|' +
  'Stop use and ask a doctor|If pregnant or breast-feeding|Keep out of reach of children|' +
  'In case of overdose';

// FDA warnings/interactions text is one dense, unpunctuated block. Break it into
// readable points: split on sentence boundaries, before short labeled sections
// like "Allergy alert :" or "Stomach bleeding warning:", and before the standard
// Drug Facts subheadings above.
@Pipe({ name: 'fdaPoints', standalone: true })
export class FdaPointsPipe implements PipeTransform {
  transform(text: string | null | undefined): string[] {
    if (!text) return [];

    const cleaned = stripLabelPrefix(text);
    const splitPattern = new RegExp(
      `(?<=[.!?])\\s+(?=[A-Z0-9])|\\s+(?=[A-Z][a-zA-Z' ]{2,40}\\s*:)|\\s+(?=${STANDARD_HEADINGS})`
    );
    const parts = cleaned.split(splitPattern);

    return parts.map((p) => p.trim()).filter((p) => p.length > 0);
  }
}
