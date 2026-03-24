import createAjaxRequest from 'Utilities/createAjaxRequest';
import updateVideos from 'Utilities/Video/updateVideos';
import getSectionState from 'Utilities/State/getSectionState';

function createBatchToggleVideoMonitoredHandler(section, fetchHandler) {
  return function(getState, payload, dispatch) {
    const {
      videoIds,
      monitored
    } = payload;

    const state = getSectionState(getState(), section, true);

    dispatch(updateVideos(section, state.items, videoIds, {
      isSaving: true,
      monitored
    }));

    const promise = createAjaxRequest({
      url: '/videos/monitor',
      method: 'PUT',
      data: JSON.stringify({ videoIds, monitored }),
      dataType: 'json'
    }).request;

    promise.done(() => {
      dispatch(updateVideos(section, state.items, videoIds, {
        isSaving: false,
        monitored
      }));

      dispatch(fetchHandler());
    });

    promise.fail(() => {
      dispatch(updateVideos(section, state.items, videoIds, {
        isSaving: false,
        monitored: !monitored
      }));
    });
  };
}

export default createBatchToggleVideoMonitoredHandler;
