# Brief

Fluid simulation from Lagrangian perspective, modeling the behavior of fluids by tracking individual particles as they move through space and time.

## Showcases

![showcase1](https://raw.gitmirror.com/congyuxiaoyoudao/Picgo-ImageBed/main/TA-issues/Issue%2012.SPHFluidSimulation/202506181524983.gif)

![showcase2](https://raw.gitmirror.com/congyuxiaoyoudao/Picgo-ImageBed/main/TA-issues/Issue%2012.SPHFluidSimulation/202506181524739.gif)

![showcase3](https://raw.gitmirror.com/congyuxiaoyoudao/Picgo-ImageBed/main/TA-issues/Issue%2012.SPHFluidSimulation/202506181524508.gif)

This project also has a detailed document [here](https://zhuanlan.zhihu.com/p/1918706515377890940).

## Simulating

- Idea: SPH(Smoothed Particle Hydrodynamics)
- Accelerated by compute shader
- Spatial hashing
- GPU radix sort
 
## Rendering

- Idea: PBF(Position-Based Fluids)
- Screen space composition
- Accelerated by compute shader(bilateral filter, normal reconstruction) 
- Dispatch custom passes using unity renderer feature

# TODO

- Caustic: photon mapping
- Interaction
- Form, spray
- ...

# Reference

- [Particle-based fluid simulation for interactive applications](https://dl.acm.org/doi/10.5555/846276.846298)
- [Screen Space Fluid Rendering for Games](https://developer.download.nvidia.com/presentations/2010/gdc/Direct3D_Effects.pdf)
- [Position based fluids](https://dl.acm.org/doi/10.1145/2461912.2461984)