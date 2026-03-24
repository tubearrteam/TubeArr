import _ from 'lodash';
import { createSelector } from 'reselect';
import videoEntities from 'Video/videoEntities';

function createVideoSelector() {
  return createSelector(
    (state, { videoId }) => videoId,
    (state, { videoEntity = videoEntities.VIDEOS }) => _.get(state, videoEntity, { items: [] }),
    (videoId, videos) => {
      return _.find(videos.items, { id: videoId });
    }
  );
}

export default createVideoSelector;
