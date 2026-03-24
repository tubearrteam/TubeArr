/**
 * Channel input classifier tests (search protection).
 * Run from repo root: node frontend/src/Utilities/Channel/__tests__/channelInputClassifier.test.js
 * Or with Jest if configured: npm test -- channelInputClassifier
 */

const path = require('path');
const fs = require('fs');

function runTests() {
  // Load the classifier via require by resolving built or source
  let classifyInput, isResolvableWithoutSearch, hasDirectChannelId, InputKind, normalizeInput;
  try {
    const mod = require(path.join(__dirname, '..', 'channelInputClassifier.js'));
    classifyInput = mod.classifyInput;
    isResolvableWithoutSearch = mod.isResolvableWithoutSearch;
    hasDirectChannelId = mod.hasDirectChannelId;
    InputKind = mod.InputKind;
    normalizeInput = mod.normalizeInput;
  } catch (e) {
    console.error('Could not load channelInputClassifier. Run from repo root or ensure Babel/Node can load ES module.');
    console.error(e.message);
    process.exit(1);
  }

  let passed = 0;
  let failed = 0;

  function assertEqual(actual, expected, name) {
    const ok = actual === expected || (typeof expected === 'object' && actual && expected && JSON.stringify(actual) === JSON.stringify(expected));
    if (ok) {
      passed++;
    } else {
      failed++;
      console.error('FAIL', name, '\n  expected:', expected, '\n  actual:', actual);
    }
  }

  function assertKind(input, expectedKind, name) {
    const c = classifyInput(input);
    assertEqual(c.kind, expectedKind, name || input);
  }

  // Direct UC... channel ID
  assertKind('UC123456789012345678901', InputKind.ChannelId, 'direct UC id');
  assertKind('  UCabcdefghijklmnopqrstuv  ', InputKind.ChannelId, 'trimmed UC id');
  assertEqual(classifyInput('UC123456789012345678901').channelId, 'UC123456789012345678901', 'channelId extracted');

  // /channel/UC... URL
  assertKind('https://www.youtube.com/channel/UC123456789012345678901', InputKind.ChannelUrl, 'channel URL');
  assertKind('youtube.com/channel/UCabcdefghijklmnopqrstuv', InputKind.ChannelUrl, 'channel URL no protocol');
  assertEqual(classifyInput('https://www.youtube.com/channel/UC123456789012345678901').channelId, 'UC123456789012345678901', 'channelId from URL');

  // @handle
  assertKind('@CorridorCrew', InputKind.HandlePath, '@handle');
  assertKind('https://www.youtube.com/@CorridorCrew', InputKind.HandleUrl, 'handle URL');
  assertKind('https://www.youtube.com/@name/videos', InputKind.HandleUrl, 'handle /videos URL');

  // Legacy
  assertKind('https://www.youtube.com/user/olduser', InputKind.LegacyUserUrl, 'legacy /user/ URL');
  assertKind('https://www.youtube.com/c/custom', InputKind.LegacyCustomUrl, 'legacy /c/ URL');

  // Search term
  assertKind('Corridor Crew', InputKind.SearchTerm, 'plain search term');
  assertKind('some random text', InputKind.SearchTerm, 'plain text');

  // Empty
  assertKind('', InputKind.Empty, 'empty');
  assertKind('   ', InputKind.Empty, 'whitespace');

  // Resolvable without search
  assertEqual(isResolvableWithoutSearch(classifyInput('UC123456789012345678901')), true, 'UC is resolvable');
  assertEqual(isResolvableWithoutSearch(classifyInput('https://www.youtube.com/channel/UC123456789012345678901')), true, 'channel URL is resolvable');
  assertEqual(isResolvableWithoutSearch(classifyInput('@handle')), true, 'handle is resolvable');
  assertEqual(isResolvableWithoutSearch(classifyInput('Corridor Crew')), false, 'search term not resolvable');

  // Direct channel id helper
  assertEqual(hasDirectChannelId(classifyInput('UC123456789012345678901')), true, 'hasDirectChannelId UC');
  assertEqual(hasDirectChannelId(classifyInput('https://www.youtube.com/channel/UC123456789012345678901')), true, 'hasDirectChannelId URL');
  assertEqual(hasDirectChannelId(classifyInput('@x')), false, 'hasDirectChannelId handle false');

  console.log('Channel input classifier tests:', passed, 'passed', failed, 'failed');
  process.exit(failed > 0 ? 1 : 0);
}

runTests();
