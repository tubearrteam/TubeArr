import _ from 'lodash';
import PropTypes from 'prop-types';
import React, { Component, cloneElement, isValidElement } from 'react';
import ReactMeasure from 'react-measure';

class Measure extends Component {

  //
  // Lifecycle

  componentWillUnmount() {
    this.onResize.cancel();
  }

  //
  // Listeners

  onResize = _.debounce((contentRect) => {
    const whitelist = this.props.whitelist || ['width', 'height'];

    const payload = {};
    const bounds = contentRect && contentRect.bounds ? contentRect.bounds : {};

    if (whitelist.includes('width')) {
      payload.width = bounds.width || 0;
    }

    if (whitelist.includes('height')) {
      payload.height = bounds.height || 0;
    }

    this.props.onMeasure(payload);
  }, 250, { leading: true, trailing: false });

  //
  // Render

  render() {
    const {
      children,
      onMeasure,
      whitelist,
      ...otherProps
    } = this.props;

    const childAcceptsRef = (child) => {
      if (!isValidElement(child)) {
        return false;
      }

      // DOM element
      if (typeof child.type === 'string') {
        return true;
      }

      // Class component
      if (typeof child.type === 'function' && child.type.prototype && child.type.prototype.isReactComponent) {
        return true;
      }

      // forwardRef component
      return child.type && child.type.$$typeof === Symbol.for('react.forward_ref');
    };

    return (
      <ReactMeasure
        bounds
        onResize={this.onResize}
        {...otherProps}
      >
        {({ measureRef, contentRect, measure }) => {
          // Support react-measure render-prop usage if it exists anywhere.
          if (typeof children === 'function') {
            return children({ measure, measureRef, contentRect });
          }

          // If we have a single child that can take a ref, attach it directly.
          if (childAcceptsRef(children)) {
            return cloneElement(children, { ref: measureRef });
          }

          // Fallback: wrap in a div to attach the ref.
          return <div ref={measureRef}>{children}</div>;
        }}
      </ReactMeasure>
    );
  }
}

Measure.propTypes = {
  onMeasure: PropTypes.func.isRequired,
  whitelist: PropTypes.arrayOf(PropTypes.oneOf(['width', 'height'])),
  children: PropTypes.oneOfType([PropTypes.node, PropTypes.func])
};

export default Measure;
