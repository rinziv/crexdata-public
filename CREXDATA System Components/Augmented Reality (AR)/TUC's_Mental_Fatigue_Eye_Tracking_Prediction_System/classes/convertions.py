import numpy as np


class Convertions:

    def __init__(
        self,
        VERTICAL,
        HORIZONTAL,
    ):
        self.VERTICAL = VERTICAL
        self.HORIZONTAL = HORIZONTAL

    def pxl2deg(self, prevCentroid, centroid, prevTime, currTime, prevVelocity):
        angDistance = np.sqrt(
            (centroid[0] * self.VERTICAL - prevCentroid[0] * self.VERTICAL) ** 2
            + (centroid[1] * self.HORIZONTAL - prevCentroid[1] * self.HORIZONTAL) ** 2
        )
        angVelocity = angDistance / (currTime - prevTime)
        angAcceleration = (angVelocity - prevVelocity) / (currTime - prevTime)
        return angDistance, angVelocity, angAcceleration
