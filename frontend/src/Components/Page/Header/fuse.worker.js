import Fuse from 'fuse.js';

const fuseOptions = {
  shouldSort: true,
  includeMatches: true,
  ignoreLocation: true,
  threshold: 0.3,
  maxPatternLength: 32,
  minMatchCharLength: 1,
  keys: [
    'title',
    'alternateTitles.title',
    'youtubeChannelId',
    'tags.label'
  ]
};

function getSuggestions(channels, value) {
  const limit = 10;
  let suggestions = [];

  if (value.length === 1) {
    for (let i = 0; i < channels.length; i++) {
      const s = channels[i];
      if (s.firstCharacter === value.toLowerCase()) {
        suggestions.push({
          item: channels[i],
          indices: [
            [0, 0]
          ],
          matches: [
            {
              value: s.title,
              key: 'title'
            }
          ],
          refIndex: 0
        });
        if (suggestions.length > limit) {
          break;
        }
      }
    }
  } else {
    const fuse = new Fuse(channels, fuseOptions);
    suggestions = fuse.search(value, { limit });
  }

  return suggestions;
}

onmessage = function(e) {
  if (!e) {
    return;
  }

  const {
    channels,
    value
  } = e.data;

  const suggestions = getSuggestions(channels, value);

  const results = {
    value,
    suggestions
  };

  self.postMessage(results);
};
