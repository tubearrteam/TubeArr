import createAjaxRequest from 'Utilities/createAjaxRequest';

function getTranslations() {
  return createAjaxRequest({
    global: false,
    dataType: 'json',
    url: '/localization',
  }).request;
}

let translations: Record<string, string> = {};

export async function fetchTranslations(): Promise<boolean> {
  return new Promise(async (resolve) => {
    try {
      const data = await getTranslations();
      const strings = data?.strings;
      if (!strings || typeof strings !== 'object' || Array.isArray(strings)) {
        resolve(false);
        return;
      }
      translations = strings as Record<string, string>;

      resolve(true);
    } catch {
      resolve(false);
    }
  });
}

export default function translate(
  key: string,
  tokens: Record<string, string | number | boolean> = {}
) {
  const { isProduction = true } = window.TubeArr;

  if (!isProduction && !(key in translations)) {
    console.warn(`Missing translation for key: ${key}`);
  }

  const translation = translations[key] || key;

  tokens.appName = 'TubeArr';

  return translation.replace(/\{([a-z0-9]+?)\}/gi, (match, tokenMatch) =>
    String(tokens[tokenMatch] ?? match)
  );
}
