import numpy as np


class FrameLabeler:
    def __init__(self, blink_threshold=0.8):
        """
        Initializes the FrameLabeler with a default threshold for blinking.

        :param blink_threshold: Threshold for classifying a frame as 'blink'
        """
        self.blink_threshold = blink_threshold

    def label_frame(self, blink_probability, centroid, angDistVelAcc):
        # Check if frame should be labeled as 'blink'
        if blink_probability >= self.blink_threshold:
            # and (np.isnan(centroid).any()):
            return "blink"

        # Check for 'flicker'
        elif angDistVelAcc[1] > 1000 or abs(angDistVelAcc[2]) > 100000:
            return "flicker"

        # Check for 'saccade'
        elif angDistVelAcc[1] > 240 and abs(angDistVelAcc[2]) > 3000:
            # print("Saccade")
            return "saccade"

        # Check for 'fixation'
        elif angDistVelAcc[1] < 240 and abs(angDistVelAcc[2]) < 3000:
            return "fixation"

        # Default label if no other conditions are met
        return "normal"
